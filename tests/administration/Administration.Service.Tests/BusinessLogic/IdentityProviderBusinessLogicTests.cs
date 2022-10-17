/********************************************************************************
 * Copyright (c) 2021,2022 BMW Group AG
 * Copyright (c) 2021,2022 Contributors to the CatenaX (ng) GitHub Organisation.
 *
 * See the NOTICE file(s) distributed with this work for additional
 * information regarding copyright ownership.
 *
 * This program and the accompanying materials are made available under the
 * terms of the Apache License, Version 2.0 which is available at
 * https://www.apache.org/licenses/LICENSE-2.0.
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * SPDX-License-Identifier: Apache-2.0
 ********************************************************************************/

using AutoFixture;
using AutoFixture.AutoFakeItEasy;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Org.CatenaX.Ng.Portal.Backend.Framework.IO;
using Org.CatenaX.Ng.Portal.Backend.PortalBackend.DBAccess;
using Org.CatenaX.Ng.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.CatenaX.Ng.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.CatenaX.Ng.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.CatenaX.Ng.Portal.Backend.Provisioning.Library;
using Org.CatenaX.Ng.Portal.Backend.Provisioning.Library.Models;
using System.Text;
using Xunit;

namespace Org.CatenaX.Ng.Portal.Backend.Administration.Service.BusinessLogic.Tests;

public class IdentityProviderBusinessLogicTests
{
    private readonly IFixture _fixture;
    private readonly IProvisioningManager _provisioningManager;
    private readonly IPortalRepositories _portalRepositories;
    private readonly IIdentityProviderRepository _identityProviderRepository;
    private readonly IUserRepository _userRepository;
    private readonly IOptions<IdentityProviderSettings> _options;
    private readonly IdentityProviderCsvSettings _csvSettings;
    private readonly IFormFile _document;
    private readonly Encoding _encoding;
    private readonly string _iamUserId;
    private readonly Guid _companyId;
    private readonly Guid _companyUserId;
    private readonly Guid _sharedIdentityProviderId;
    private readonly string _sharedIdpAlias;
    private readonly Guid _otherIdentityProviderId;
    private readonly string _otherIdpAlias;
    private readonly Exception _error;

    public IdentityProviderBusinessLogicTests()
    {
        _fixture = new Fixture().Customize(new AutoFakeItEasyCustomization { ConfigureMembers = true });
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        _provisioningManager = A.Fake<IProvisioningManager>();
        _portalRepositories = A.Fake<IPortalRepositories>();
        _identityProviderRepository = A.Fake<IIdentityProviderRepository>();
        _userRepository = A.Fake<IUserRepository>();
        _options = A.Fake<IOptions<IdentityProviderSettings>>();
        _document = A.Fake<IFormFile>();

        _iamUserId = _fixture.Create<string>();
        _companyId = _fixture.Create<Guid>();
        _companyUserId = _fixture.Create<Guid>();
        _sharedIdentityProviderId = _fixture.Create<Guid>();
        _sharedIdpAlias = _fixture.Create<string>();
        _otherIdentityProviderId = _fixture.Create<Guid>();
        _otherIdpAlias = _fixture.Create<string>();
        _encoding = _fixture.Create<Encoding>();

        _csvSettings = new IdentityProviderCsvSettings
            {
                Charset = "",
                Encoding = _encoding,
                FileName = _fixture.Create<string>(),
                ContentType = "text/csv",
                Separator = ",",
                HeaderUserId = "UserId",
                HeaderFirstName = "FirstName",
                HeaderLastName = "LastName",
                HeaderEmail = "Email",
                HeaderProviderAlias = "ProviderAlias",
                HeaderProviderUserId = "ProviderUserId",
                HeaderProviderUserName = "ProviderUserName"
            };

        _error = _fixture.Create<TestException>();
    }

    #region UploadOwnCompanyUsersIdentityProviderLinkDataAsync

    [Fact]
    public async void TestUploadOwnCompanyUsersIdentityProviderLinkDataAsyncAllUnchangedSuccess()
    {
        var numUsers = 5;

        var users = _fixture.CreateMany<TestUserData>(numUsers).ToList();

        var lines = new [] { HeaderLine() }.Concat(users.Select(u => NextLine(u)));

        SetupFakes(users,lines);

        var sut = new IdentityProviderBusinessLogic(
            _portalRepositories,
            _provisioningManager,
            _options);

        var result = await sut.UploadOwnCompanyUsersIdentityProviderLinkDataAsync(_document,_iamUserId,CancellationToken.None).ConfigureAwait(false);
        result.Updated.Should().Be(0);
        result.Unchanged.Should().Be(numUsers);
        result.Error.Should().Be(0);
        result.Total.Should().Be(numUsers);
        result.Errors.Should().BeEmpty();
    }

    #endregion

    #region Setup

    private void SetupFakes(IEnumerable<TestUserData> userData, IEnumerable<string> lines)
    {
        A.CallTo(() => _options.Value).Returns(new IdentityProviderSettings { CsvSettings = _csvSettings });

        A.CallTo(() => _document.ContentType).Returns(_options.Value.CsvSettings.ContentType);
        A.CallTo(() => _document.OpenReadStream()).ReturnsLazily(() => new AsyncEnumerableStringStream(lines.ToAsyncEnumerable(), _encoding));

        A.CallTo(() => _portalRepositories.GetInstance<IUserRepository>()).Returns(_userRepository);
        A.CallTo(() => _portalRepositories.GetInstance<IIdentityProviderRepository>()).Returns(_identityProviderRepository);

        A.CallTo(() => _userRepository.GetOwnCompanyAndCompanyUserId(A<string>.That.Not.IsEqualTo(_iamUserId))).Returns(
            ((Guid companyId,Guid companyUserId))default);
        A.CallTo(() => _userRepository.GetOwnCompanyAndCompanyUserId(A<string>.That.IsEqualTo(_iamUserId))).Returns(
            (companyId: _companyId, companyUserId: _companyUserId));

        A.CallTo(() => _userRepository.GetUserEntityDataAsync(A<Guid>._,A<Guid>._)).ReturnsLazily((Guid companyUserId, Guid _) =>
            userData.Where(d => d.CompanyUserId == companyUserId)
                .Select(d =>
                    (
                        UserEntityId: d.UserEntityId,
                        FirstName: d.FirstName,
                        LastName: d.LastName,
                        Email: d.Email
                    )).First());

        A.CallTo(() => _identityProviderRepository.GetCompanyIdentityProviderCategoryDataUntracked(A<Guid>.That.Not.IsEqualTo(_companyId))).Returns(
            Enumerable.Empty<(Guid IdentityProviderId, IdentityProviderCategoryId CategoryId, string Alias)>().ToAsyncEnumerable());
        A.CallTo(() => _identityProviderRepository.GetCompanyIdentityProviderCategoryDataUntracked(A<Guid>.That.IsEqualTo(_companyId))).Returns(
            new [] {
                (IdentityProviderId: _sharedIdentityProviderId, CategoryId: IdentityProviderCategoryId.KEYCLOAK_SHARED, Alias: _sharedIdpAlias),
                (IdentityProviderId: _otherIdentityProviderId, CategoryId: IdentityProviderCategoryId.KEYCLOAK_OIDC, Alias: _otherIdpAlias),
            }.ToAsyncEnumerable());

        A.CallTo(() => _identityProviderRepository.CreateIdentityProvider(A<IdentityProviderCategoryId>._)).ReturnsLazily((IdentityProviderCategoryId categoryId) =>
            new IdentityProvider(_sharedIdentityProviderId,categoryId,_fixture.Create<DateTimeOffset>()));

        A.CallTo(() => _provisioningManager.GetProviderUserLinkDataForCentralUserIdAsync(A<string>._)).ReturnsLazily((string userEntityId) =>
        {
            var user = userData.First(u => u.UserEntityId == userEntityId);
            return new [] {
                new IdentityProviderLink(
                    _sharedIdpAlias,
                    user.SharedIdpUserId,
                    user.SharedIdpUserName
                ),
                new IdentityProviderLink(
                    _otherIdpAlias,
                    user.OtherIdpUserId,
                    user.OtherIdpUserName
                )
            }.ToAsyncEnumerable();
        });
    }

    private string HeaderLine()
    {
        return string.Join(",", new [] {
            _csvSettings.HeaderUserId,
            _csvSettings.HeaderFirstName,
            _csvSettings.HeaderLastName,
            _csvSettings.HeaderEmail,
            _csvSettings.HeaderProviderAlias,
            _csvSettings.HeaderProviderUserId,
            _csvSettings.HeaderProviderUserName,
            _csvSettings.HeaderProviderAlias,
            _csvSettings.HeaderProviderUserId,
            _csvSettings.HeaderProviderUserName
        });
    }

    private string NextLine(TestUserData userData)
    {
        return string.Join(",", new [] {
            userData.CompanyUserId.ToString(),
            userData.FirstName,
            userData.LastName,
            userData.Email,
            _sharedIdpAlias,
            userData.SharedIdpUserId,
            userData.SharedIdpUserName,
            _otherIdpAlias,
            userData.OtherIdpUserId,
            userData.OtherIdpUserName
        });
    }

    private record TestUserData(Guid CompanyUserId, string UserEntityId, string FirstName, string LastName, string Email, string SharedIdpUserId, string SharedIdpUserName, string OtherIdpUserId, string OtherIdpUserName);

    #endregion

    [Serializable]
    public class TestException : Exception
    {
        public TestException() { }
        public TestException(string message) : base(message) { }
        public TestException(string message, Exception inner) : base(message, inner) { }
        protected TestException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
