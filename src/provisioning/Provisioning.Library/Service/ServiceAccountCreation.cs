/********************************************************************************
 * Copyright (c) 2022 Contributors to the Eclipse Foundation
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

using Microsoft.Extensions.Options;
using Org.Eclipse.TractusX.Portal.Backend.Framework.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Framework.Linq;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Models;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.DBAccess.Repositories;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Entities;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Enums;
using Org.Eclipse.TractusX.Portal.Backend.PortalBackend.PortalEntities.Extensions;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.DBAccess;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Enums;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.ErrorHandling;
using Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Models;
using System.Collections.Immutable;
using ServiceAccountData = Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Models.ServiceAccountData;

namespace Org.Eclipse.TractusX.Portal.Backend.Provisioning.Library.Service;

/// <inheritdoc />
public class ServiceAccountCreation(
    IProvisioningManager provisioningManager,
    IPortalRepositories portalRepositories,
    IProvisioningDBAccess provisioningDbAccess,
    IOptions<ServiceAccountCreationSettings> options) : IServiceAccountCreation
{
    private readonly ServiceAccountCreationSettings _settings = options.Value;

    /// <inheritdoc />
    async Task<(bool HasExternalServiceAccount, IEnumerable<CreatedServiceAccountData> ServiceAccounts)> IServiceAccountCreation.CreateServiceAccountAsync(ServiceAccountCreationInfo creationData,
            Guid companyId,
            IEnumerable<string> bpns,
            CompanyServiceAccountTypeId companyServiceAccountTypeId,
            bool enhanceTechnicalUserName,
            bool enabled,
            ServiceAccountCreationProcessData? processData,
            Action<CompanyServiceAccount>? setOptionalParameter)
    {
        var (name, description, iamClientAuthMethod, userRoleIds) = creationData;
        var serviceAccountsRepository = portalRepositories.GetInstance<IServiceAccountRepository>();
        var userRolesRepository = portalRepositories.GetInstance<IUserRolesRepository>();

        var userRoleData = await GetAndValidateUserRoleData(userRolesRepository, userRoleIds).ConfigureAwait(ConfigureAwaitOptions.None);
        var serviceAccounts = ImmutableList.CreateBuilder<CreatedServiceAccountData>();

        var (clientId, enhancedName, serviceAccountData) = await CreateKeycloakServiceAccount(bpns, enhanceTechnicalUserName, enabled, name, description, iamClientAuthMethod, userRoleData).ConfigureAwait(ConfigureAwaitOptions.None);
        var serviceAccountId = CreateDatabaseServiceAccount(companyId, UserStatusId.ACTIVE, companyServiceAccountTypeId, CompanyServiceAccountKindId.INTERNAL, name, clientId, description, userRoleData, serviceAccountsRepository, userRolesRepository, setOptionalParameter);
        serviceAccounts.Add(new CreatedServiceAccountData(
            serviceAccountId,
            enhancedName,
            description,
            UserStatusId.ACTIVE,
            clientId,
            serviceAccountData,
            userRoleData));

        var dimRoles = userRoleData
                .Join(_settings.DimUserRoles, data => data.ClientClientId, config => config.ClientId,
                    (data, config) => new { data, config })
                .Where(@t => t.config.UserRoleNames.Contains(@t.data.UserRoleText))
                .Select(@t => t.data)
                .ToImmutableList();

        var hasExternalServiceAccount = dimRoles.IfAny(roles =>
            {
                var dimSaName = $"dim-{name}";
                var dimServiceAccountId = CreateDatabaseServiceAccount(companyId, UserStatusId.PENDING, companyServiceAccountTypeId, CompanyServiceAccountKindId.EXTERNAL, dimSaName, null, description, roles, serviceAccountsRepository, userRolesRepository, setOptionalParameter);
                var processStepRepository = portalRepositories.GetInstance<IProcessStepRepository>();
                if (processData?.ProcessTypeId is not null)
                {
                    Guid processId;
                    if (processData.ProcessId is null)
                    {
                        var process = processStepRepository.CreateProcess(processData.ProcessTypeId.Value);
                        processStepRepository.CreateProcessStep(processData.ProcessTypeId.Value.GetInitialProcessStepTypeIdForSaCreation(), ProcessStepStatusId.TODO, process.Id);
                        processId = process.Id;
                    }
                    else
                    {
                        processId = processData.ProcessId.Value;
                    }

                    portalRepositories.GetInstance<IServiceAccountRepository>().CreateDimUserCreationData(serviceAccountId, processId);
                }

                serviceAccounts.Add(new CreatedServiceAccountData(
                    dimServiceAccountId,
                    dimSaName,
                    description,
                    UserStatusId.PENDING,
                    null,
                    null,
                    roles));
            });

        return (hasExternalServiceAccount, serviceAccounts.ToImmutable());
    }

    private static async Task<IEnumerable<UserRoleData>> GetAndValidateUserRoleData(IUserRolesRepository userRolesRepository, IEnumerable<Guid> userRoleIds)
    {
        var userRoleData = await userRolesRepository
            .GetUserRoleDataUntrackedAsync(userRoleIds).ToListAsync().ConfigureAwait(false);
        if (userRoleData.Count != userRoleIds.Count())
        {
            userRoleIds.Except(userRoleData.Select(x => x.UserRoleId)).IfAny(missingRoleIds =>
                throw NotFoundException.Create(ProvisioningServiceErrors.USER_NOT_VALID_USERROLEID, [new("missingRoleIds", string.Join(", ", missingRoleIds))]));
        }

        return userRoleData;
    }

    private Guid CreateDatabaseServiceAccount(
        Guid companyId,
        UserStatusId userStatusId,
        CompanyServiceAccountTypeId companyServiceAccountTypeId,
        CompanyServiceAccountKindId companyServiceAccountKindId,
        string name,
        string? clientId,
        string description,
        IEnumerable<UserRoleData> userRoleData,
        IServiceAccountRepository serviceAccountsRepository,
        IUserRolesRepository userRolesRepository,
        Action<CompanyServiceAccount>? setOptionalParameter)
    {
        var identity = portalRepositories.GetInstance<IUserRepository>().CreateIdentity(companyId, userStatusId, IdentityTypeId.COMPANY_SERVICE_ACCOUNT, null);
        var serviceAccount = serviceAccountsRepository.CreateCompanyServiceAccount(
            identity.Id,
            name,
            description,
            clientId,
            companyServiceAccountTypeId,
            companyServiceAccountKindId,
            setOptionalParameter);

        userRolesRepository.CreateIdentityAssignedRoleRange(
            userRoleData.Select(x => (identity.Id, x.UserRoleId)));

        return serviceAccount.Id;
    }

    private async Task<(string clientId, string enhancedName, ServiceAccountData serviceAccountData)> CreateKeycloakServiceAccount(IEnumerable<string> bpns, bool enhanceTechnicalUserName, bool enabled,
        string name, string description, IamClientAuthMethod iamClientAuthMethod, IEnumerable<UserRoleData> userRoleData)
    {
        var clientId = await GetNextServiceAccountClientIdWithIdAsync().ConfigureAwait(ConfigureAwaitOptions.None);
        var enhancedName = enhanceTechnicalUserName ? $"{clientId}-{name}" : name;
        var serviceAccountData = await provisioningManager.SetupCentralServiceAccountClientAsync(
            clientId,
            new ClientConfigRolesData(
                enhancedName,
                description,
                iamClientAuthMethod,
                userRoleData
                    .GroupBy(userRole =>
                        userRole.ClientClientId)
                    .ToDictionary(group =>
                            group.Key,
                        group => group.Select(userRole => userRole.UserRoleText))),
            enabled).ConfigureAwait(ConfigureAwaitOptions.None);

        if (bpns.IfAny(async businessPartnerNumbers =>
        {
            await provisioningManager.AddBpnAttributetoUserAsync(serviceAccountData.IamUserId, businessPartnerNumbers).ConfigureAwait(ConfigureAwaitOptions.None);
            await provisioningManager.AddProtocolMapperAsync(serviceAccountData.InternalClientId).ConfigureAwait(ConfigureAwaitOptions.None);
        }, out var bpnTask))
        {
            await bpnTask!.ConfigureAwait(ConfigureAwaitOptions.None);
        }

        return (clientId, enhancedName, serviceAccountData);
    }

    private async Task<string> GetNextServiceAccountClientIdWithIdAsync()
    {
        var id = await provisioningDbAccess.GetNextClientSequenceAsync().ConfigureAwait(ConfigureAwaitOptions.None);
        return $"{_settings.ServiceAccountClientPrefix}{id}";
    }
}
