﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VCSVersion.AssemblyVersioning;
using VCSVersion.Configuration;
using VCSVersion.Helpers;
using VCSVersion.VCS;
using VCSVersion.VersionCalculation;
using VCSVersion.VersionCalculation.BaseVersionCalculation;
using VCSVersion.VersionCalculation.IncrementStrategies;

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable MemberCanBePrivate.Global

namespace HgVersion.Configuration
{
    public static class HgConfigurationProvider
    {
        internal const string DefaultTagPrefix = "[vV]";

        public const string DefaultConfigFileName = "HgVersion.yml";
        public const string ReleaseBranchRegex = "releases?[/-]";
        public const string FeatureBranchRegex = "features?[/-]";
        public const string PullRequestRegex = @"(pull|pull\-requests|pr)[/-]";
        public const string HotfixBranchRegex = "hotfix(es)?[/-]";
        public const string SupportBranchRegex = "support[/-]";
        public const string DevelopBranchRegex = "dev(elop)?(ment)?$";
        public const string DefaultBranchRegex = "default$";
        public const string DefaultBranchKey = "default";
        public const string ReleaseBranchKey = "release";
        public const string FeatureBranchKey = "feature";
        public const string PullRequestBranchKey = "pull-request";
        public const string HotfixBranchKey = "hotfix";
        public const string SupportBranchKey = "support";
        public const string DevelopBranchKey = "develop";

        private const IncrementStrategyType DefaultIncrementStrategy = IncrementStrategyType.Inherit;

        public static Config Provide(IRepository repository, IFileSystem fileSystem, bool applyDefaults = true, Config overrideConfig = null)
        {
            var workingDirectory = repository.Path;
            var projectRootDirectory = GetProjectRootDirectory(workingDirectory);

            if (HasConfigFileAt(workingDirectory, fileSystem))
            {
                return Provide(workingDirectory, fileSystem, applyDefaults, overrideConfig);
            }

            return Provide(projectRootDirectory, fileSystem, applyDefaults, overrideConfig);
        }

        public static Config Provide(string workingDirectory, IFileSystem fileSystem, bool applyDefaults = true, Config overrideConfig = null)
        {
            var readConfig = ReadConfig(workingDirectory, fileSystem);
            VerifyConfiguration(readConfig);

            if (applyDefaults)
                ApplyDefaultsTo(readConfig);
            if (null != overrideConfig)
                ApplyOverridesTo(readConfig, overrideConfig);

            return readConfig;
        }
        
        private static string GetProjectRootDirectory(string workingDirectory)
        {
            var dotHgDirectory = GetDotHgDirectory(workingDirectory);
            var directoryInfo = Directory.GetParent(dotHgDirectory);
            
            return directoryInfo.FullName;
        }

        private static string GetDotHgDirectory(string workingDirectory)
        {
            var directories = Directory.GetDirectories(workingDirectory, ".hg");

            if (directories.Length == 0)
                throw new DirectoryNotFoundException("Can not find the .hg directory in " + workingDirectory);

            if (directories.Length > 1)
            {
                return directories
                    .OrderBy(dir => dir.Length)
                    .First();
            }

            return directories.First();
        }

        private static void VerifyConfiguration(Config readConfig)
        {
            if (readConfig == null) 
                throw new ArgumentNullException(nameof(readConfig));
            
            // Verify no branches are set to mainline mode
            if (readConfig.Branches.Any(b => b.Value.VersioningMode == VersioningMode.Mainline))
            {
                throw new ConfigurationException(
                    "Mainline mode only works at the repository level, a single branch " +
                    "cannot be put into mainline mode. This is because mainline mode " + 
                    "treats your entire Mercurial repository as an event source with " +
                    "each merge into the 'mainline' incrementing the version.");
            }
        }

        public static void ApplyDefaultsTo(Config config)
        {
            config.AssemblyVersioningScheme = config.AssemblyVersioningScheme ?? AssemblyVersioningScheme.MajorMinorPatch;
            config.AssemblyFileVersioningScheme = config.AssemblyFileVersioningScheme ?? AssemblyFileVersioningScheme.MajorMinorPatch;
            config.AssemblyInformationalFormat = config.AssemblyInformationalFormat;
            config.TagPrefix = config.TagPrefix ?? DefaultTagPrefix;
            config.VersioningMode = config.VersioningMode ?? VersioningMode.ContinuousDelivery;
            config.ContinuousDeploymentFallbackTag = config.ContinuousDeploymentFallbackTag ?? "ci";
            config.MajorVersionBumpMessage = config.MajorVersionBumpMessage ?? IncrementStrategy.DefaultMajorPattern;
            config.MinorVersionBumpMessage = config.MinorVersionBumpMessage ?? IncrementStrategy.DefaultMinorPattern;
            config.PatchVersionBumpMessage = config.PatchVersionBumpMessage ?? IncrementStrategy.DefaultPatchPattern;
            config.NoBumpMessage = config.NoBumpMessage ?? IncrementStrategy.DefaultNoBumpPattern;
            config.CommitMessageIncrementing = config.CommitMessageIncrementing ?? CommitMessageIncrementMode.Enabled;
            config.BuildMetaDataPadding = config.BuildMetaDataPadding ?? 4;
            config.CommitsSinceVersionSourcePadding = config.CommitsSinceVersionSourcePadding ?? 4;
            config.CommitDateFormat = config.CommitDateFormat ?? "yyyy-MM-dd";
            config.BaseVersionStrategies = config.BaseVersionStrategies ?? GetDefaultBaseVersionStrategies();
            config.TaggedCommitsLimit = config.TaggedCommitsLimit ?? 10;
            
            var configBranches = config.Branches.ToList();

            ApplyBranchDefaults(config,
                GetOrCreateBranchDefaults(config, DevelopBranchKey),
                DevelopBranchRegex,
                new List<string>(),
                defaultTag: "alpha",
                defaultIncrementStrategy: IncrementStrategyType.Minor,
                defaultVersioningMode: VersioningMode.ContinuousDeployment,
                defaultTrackMergeTarget: true,
                tracksReleaseBranches: true);
            ApplyBranchDefaults(config,
                GetOrCreateBranchDefaults(config, DefaultBranchKey),
                DefaultBranchRegex,
                new List<string> { DevelopBranchKey, ReleaseBranchKey },
                defaultTag: string.Empty,
                defaultPreventIncrement: true,
                defaultIncrementStrategy: IncrementStrategyType.Patch,
                isMainline: true);
            ApplyBranchDefaults(config,
                GetOrCreateBranchDefaults(config, ReleaseBranchKey),
                ReleaseBranchRegex,
                new List<string> { DevelopBranchKey, DefaultBranchKey, SupportBranchKey, ReleaseBranchKey },
                defaultTag: "beta",
                defaultPreventIncrement: true,
                defaultIncrementStrategy: IncrementStrategyType.Patch,
                isReleaseBranch: true);
            ApplyBranchDefaults(config,
                GetOrCreateBranchDefaults(config, FeatureBranchKey),
                FeatureBranchRegex,
                new List<string> { DevelopBranchKey, DefaultBranchKey, ReleaseBranchKey, FeatureBranchKey, SupportBranchKey, HotfixBranchKey },
                defaultIncrementStrategy: IncrementStrategyType.Inherit);
            ApplyBranchDefaults(config,
                GetOrCreateBranchDefaults(config, PullRequestBranchKey),
                PullRequestRegex,
                new List<string> { DevelopBranchKey, DefaultBranchKey, ReleaseBranchKey, FeatureBranchKey, SupportBranchKey, HotfixBranchKey },
                defaultTag: "PullRequest",
                defaultTagNumberPattern: @"[/-](?<number>\d+)",
                defaultIncrementStrategy: IncrementStrategyType.Inherit);
            ApplyBranchDefaults(config,
                GetOrCreateBranchDefaults(config, HotfixBranchKey),
                HotfixBranchRegex,
                new List<string> { DevelopBranchKey, DefaultBranchKey, SupportBranchKey },
                defaultTag: "beta",
                defaultIncrementStrategy: IncrementStrategyType.Patch);
            ApplyBranchDefaults(config,
                GetOrCreateBranchDefaults(config, SupportBranchKey),
                SupportBranchRegex,
                new List<string> { DefaultBranchKey },
                defaultTag: string.Empty,
                defaultPreventIncrement: true,
                defaultIncrementStrategy: IncrementStrategyType.Patch,
                isMainline: true);

            // Any user defined branches should have other values defaulted after known branches filled in.
            // This allows users to override any of the value.
            foreach (var branchConfig in configBranches)
            {
                var regex = branchConfig.Value.Regex;
                if (regex == null)
                {
                    throw new ConfigurationException($"Branch configuration '{branchConfig.Key}' is missing required configuration 'regex'");
                }

                var sourceBranches = branchConfig.Value.SourceBranches;
                if (sourceBranches == null)
                {
                    throw new ConfigurationException($"Branch configuration '{branchConfig.Key}' is missing required configuration 'source-branches'");
                }

                ApplyBranchDefaults(config, branchConfig.Value, regex, sourceBranches);
            }

            // This is a second pass to add additional sources, it has to be another pass to prevent ordering issues
            foreach (var branchConfig in configBranches)
            {
                if (branchConfig.Value.IsSourceBranchFor == null) continue;
                foreach (var isSourceBranch in branchConfig.Value.IsSourceBranchFor)
                {
                    config.Branches[isSourceBranch].SourceBranches.Add(branchConfig.Key);
                }
            }
        }

        private static string[] GetDefaultBaseVersionStrategies()
        {
            return ConfigHelper
                .GetConfigurationAliases<IBaseVersionStrategy>()
                .ToArray();
        }

        private static void ApplyOverridesTo(Config config, Config overrideConfig)
        {
            config.TagPrefix = string.IsNullOrWhiteSpace(overrideConfig.TagPrefix) 
                ? config.TagPrefix 
                : overrideConfig.TagPrefix;
        }

        private static BranchConfig GetOrCreateBranchDefaults(Config config, string branchKey)
        {
            if (config.Branches.ContainsKey(branchKey)) 
                return config.Branches[branchKey];
            
            var branchConfig = new BranchConfig { Name = branchKey };
            config.Branches.Add(branchKey, branchConfig);
            return branchConfig;
        }

        public static void ApplyBranchDefaults(Config config,
            BranchConfig branchConfig,
            string branchRegex,
            List<string> sourceBranches,
            string defaultTag = "useBranchName",
            IncrementStrategyType? defaultIncrementStrategy = null, // Looked up from main config
            bool defaultPreventIncrement = false,
            VersioningMode? defaultVersioningMode = null, // Looked up from main config
            bool defaultTrackMergeTarget = false,
            string defaultTagNumberPattern = null,
            bool tracksReleaseBranches = false,
            bool isReleaseBranch = false,
            bool isMainline = false)
        {
            branchConfig.Regex = string.IsNullOrEmpty(branchConfig.Regex) ? branchRegex : branchConfig.Regex;
            branchConfig.SourceBranches = sourceBranches;
            branchConfig.Tag = branchConfig.Tag ?? defaultTag;
            branchConfig.TagNumberPattern = branchConfig.TagNumberPattern ?? defaultTagNumberPattern;
            branchConfig.Increment = branchConfig.Increment ?? defaultIncrementStrategy ?? config.Increment ?? DefaultIncrementStrategy;
            branchConfig.PreventIncrementOfMergedBranchVersion = branchConfig.PreventIncrementOfMergedBranchVersion ?? defaultPreventIncrement;
            branchConfig.TrackMergeTarget = branchConfig.TrackMergeTarget ?? defaultTrackMergeTarget;
            branchConfig.VersioningMode = branchConfig.VersioningMode ?? defaultVersioningMode ?? config.VersioningMode;
            branchConfig.TracksReleaseBranches = branchConfig.TracksReleaseBranches ?? tracksReleaseBranches;
            branchConfig.IsReleaseBranch = branchConfig.IsReleaseBranch ?? isReleaseBranch;
            branchConfig.IsMainline = branchConfig.IsMainline ?? isMainline;
        }

        private static Config ReadConfig(string workingDirectory, IFileSystem fileSystem)
        {
            var configFilePath = GetConfigFilePath(workingDirectory);
            
            if (!fileSystem.Exists(configFilePath))
                return new Config();
            
            var readAllText = fileSystem.ReadAllText(configFilePath);
            return ConfigSerialiser.Read(new StringReader(readAllText));
        }

        public static string GetEffectiveConfigAsString(string workingDirectory, IFileSystem fileSystem)
        {
            var config = Provide(workingDirectory, fileSystem);
            var stringBuilder = new StringBuilder();
            using (var stream = new StringWriter(stringBuilder))
            {
                ConfigSerialiser.Write(config, stream);
                stream.Flush();
            }
            return stringBuilder.ToString();
        }

        private static string GetConfigFilePath(string workingDirectory)
        {
            return Path.Combine(workingDirectory, DefaultConfigFileName);
        }

        private static bool HasConfigFileAt(string workingDirectory, IFileSystem fileSystem)
        {
            var defaultConfigFilePath = Path.Combine(workingDirectory, DefaultConfigFileName);
            return fileSystem.Exists(defaultConfigFilePath);
        }
    }
}
