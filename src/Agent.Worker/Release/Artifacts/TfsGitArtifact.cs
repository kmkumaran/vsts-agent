using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Agent.Worker.Build;
using Microsoft.VisualStudio.Services.Agent.Worker.Release.Artifacts.Definition;
using Microsoft.VisualStudio.Services.ReleaseManagement.WebApi.Contracts;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Release.Artifacts
{
    public class TfsGitArtifact : AgentService, IArtifactExtension
    {
        public Type ExtensionType => typeof(IArtifactExtension);
        public AgentArtifactType ArtifactType => AgentArtifactType.TFGit;

        public async Task DownloadAsync(IExecutionContext executionContext, ArtifactDefinition artifactDefinition, string downloadFolderPath)
        {
            ArgUtil.NotNull(executionContext, nameof(executionContext));
            ArgUtil.NotNull(artifactDefinition, nameof(artifactDefinition));
            ArgUtil.NotNullOrEmpty(downloadFolderPath, nameof(downloadFolderPath));

            var gitArtifactDetails = artifactDefinition.Details as TfsGitArtifactDetails;
            ArgUtil.NotNull(gitArtifactDetails, nameof(gitArtifactDetails));

            ServiceEndpoint endpoint = executionContext.Endpoints.FirstOrDefault((e => string.Equals(e.Name, gitArtifactDetails.RepositoryId, StringComparison.OrdinalIgnoreCase)));
            if (endpoint == null)
            {
                throw new InvalidOperationException(StringUtil.Loc("RMGitEndpointNotFound"));
            }

            var extensionManager = HostContext.GetService<IExtensionManager>();
            ISourceProvider sourceProvider = (extensionManager.GetExtensions<ISourceProvider>()).FirstOrDefault(x => x.RepositoryType == WellKnownRepositoryTypes.TfsGit);
            if (sourceProvider == null)
            {
                throw new InvalidOperationException(StringUtil.Loc("SourceArtifactProviderNotFound", WellKnownRepositoryTypes.TfsGit));
            }

            var tfsGitEndpoint = endpoint.Clone();
            tfsGitEndpoint.Data.Add(Constants.EndpointData.SourcesDirectory, downloadFolderPath);
            tfsGitEndpoint.Data.Add(Constants.EndpointData.SourceBranch, gitArtifactDetails.Branch);
            tfsGitEndpoint.Data.Add(Constants.EndpointData.SourceVersion, artifactDefinition.Version);

            await sourceProvider.GetSourceAsync(executionContext, tfsGitEndpoint, executionContext.CancellationToken);
        }

        public IArtifactDetails GetArtifactDetails(IExecutionContext context, AgentArtifactDefinition agentArtifactDefinition)
        {
            var artifactDetails = JsonConvert.DeserializeObject<Dictionary<string, string>>(agentArtifactDefinition.Details);
            var projectId = string.Empty;
            var repositoryId = string.Empty;
            var branch = string.Empty;

            if (artifactDetails.TryGetValue("ProjectId", out projectId)
                && artifactDetails.TryGetValue("RepositoryId", out repositoryId)
                && artifactDetails.TryGetValue("Branch", out branch))
            {
                return new TfsGitArtifactDetails
                {
                    RelativePath = "\\",
                    ProjectId = projectId,
                    RepositoryId = repositoryId,
                    Branch = branch
                };
            }
            else
            {
                throw new InvalidOperationException(StringUtil.Loc("RMArtifactDetailsIncomplete"));
            }
        }
    }
}