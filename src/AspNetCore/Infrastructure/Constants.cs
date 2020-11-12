#region File Header

// <copyright file="Constants.cs" company="Gibraltar Software Inc.">
// Gibraltar Software Inc. All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#endregion

namespace Loupe.Agent.AspNetCore.Infrastructure {
    internal class Constants {
        public const string SessionId = "LoupeSessionId";
        public const string AgentSessionId = "LoupeAgentSessionId";
        public const string ClientHeaderName = "loupe-agent-sessionId";
        internal const string LogSystem = "Loupe";
        internal const string Category = "Loupe.Internal";
    }
}