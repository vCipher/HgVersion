﻿using HgVersion.VCS;
using NUnit.Framework;
using Shouldly;

namespace HgVersion.Tests.VCS
{
    [TestFixture, Parallelizable(ParallelScope.All)]
    public class HgMergeMessageTests
    {
        [TestCase("Merge branch 'develop' of github.com:Particular/NServiceBus into develop", null)]
        [TestCase("Merge branch '4.0.3'", "4.0.3")] //TODO: possible make it a config option to support this
        [TestCase("Merge branch 'release-10.10.50'", "10.10.50")]
        [TestCase("Merge branch 's'", null)] // Must start with a number
        [TestCase("Merge tag '10.10.50'", "10.10.50")]
        [TestCase("Merge branch 'release-0.2.0'", "0.2.0")]
        [TestCase("Merge branch 'Release-0.2.0'", "0.2.0")]
        [TestCase("Merge branch 'Release/0.2.0'", "0.2.0")]
        [TestCase("Merge branch 'hotfix-4.6.6' into support-4.6", "4.6.6")]
        [TestCase("Merge branch 'hotfix-10.10.50'", "10.10.50")]
        [TestCase("Merge branch 'Hotfix-10.10.50'", "10.10.50")]
        [TestCase("Merge branch 'Hotfix/10.10.50'", "10.10.50")]
        [TestCase("Merge branch 'hotfix-0.1.5'", "0.1.5")]
        [TestCase("Merge branch 'hotfix-4.2.2' into support-4.2", "4.2.2")]
        [TestCase("Merge branch 'somebranch' into release-3.0.0", null)]
        [TestCase("Merge branch 'hotfix-0.1.5'\n\nRelates to: TicketId", "0.1.5")]
        [TestCase("Merge branch 'alpha-0.1.5'", "0.1.5")]
        [TestCase("Merge pull request #165 from Particular/release-1.0.0", "1.0.0")]
        [TestCase("Merge pull request #95 from Particular/issue-94", null)]
        [TestCase("Merge pull request #165 in Particular/release-1.0.0", "1.0.0")]
        [TestCase("Merge pull request #95 in Particular/issue-94", null)]
        [TestCase("Merge pull request #95 in Particular/issue-94", null)]
        [TestCase("Merge pull request #64 from arledesma/feature-VS2013_3rd_party_test_framework_support", null)]
        [TestCase("Finish Release-0.12.0", "0.12.0")] //Support Syntevo SmartGit/Hg's Gitflow merge commit messages for finishing a 'Release' branch
        [TestCase("Finish 0.14.1", "0.14.1")] //Support Syntevo SmartGit/Hg's Gitflow merge commit messages for finishing a 'Hotfix' branch
        [TestCase("Merge branch 'Release-v0.2.0'", "0.2.0")]
        [TestCase("Merge branch 'Release-v2.2'", "2.2.0")]
        [TestCase(@"Merge pull request #1 in FOO/bar from feature/ISSUE-1 to develop

        * commit '38560a7eed06e8d3f3f1aaf091befcdf8bf50fea':
          Updated jQuery to v2.1.3", null)]
        [TestCase(@"Merge pull request #45 in BRIKKS/brikks from feature/NOX-68 to develop

        * commit '38560a7eed06e8d3f3f1aaf091befcdf8bf50fea':
          Another commit message
          Commit message including a IP-number https://10.50.1.1
          A commit message", null)]
        [TestCase(@"Merge branch 'release/Sprint_2.0_Holdings_Computed_Balances'", null)]
        [TestCase(@"Merge branch 'develop' of http://10.0.6.3/gitblit/r/... into develop", null)]
        [TestCase(@"Merge branch 'master' of http://172.16.3.10:8082/r/asu_tk/p_sd", null)]
        [TestCase(@"Merge branch 'master' of http://212.248.89.56:8082/r/asu_tk/p_sd", null)]
        [TestCase(@"Merge branch 'DEMO' of http://10.10.10.121/gitlab/mtolland/orcid into DEMO", null)]
        public void ShouldParseMergeMessage(string message, string expectedVersion)
        {
            var mergeMessage = new HgMergeMessage(message, null);
            var version = mergeMessage.Version;
            version?.ToString().ShouldBe(expectedVersion);
        }
    }
}
