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

namespace CatenaX.NetworkServices.PortalBackend.DBAccess.Models
{
    public class UploadDocuments
    {
        public UploadDocuments(Guid documentId, string documentName)
        {
            DocumentId = documentId;
            DocumentName = documentName;
        }
        public Guid DocumentId { get;}

        public string DocumentName { get;}
    }

    public class RegistrationDocumentNames
    {
        public RegistrationDocumentNames(string documentName)
        {
            DocumentName = documentName;
        }
    
        public string DocumentName { get; set;}
    }
    
}
