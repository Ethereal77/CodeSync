using CodeSync.Core;

namespace CodeSync.Tests;

public sealed class GitIgnoreMatcherTests
{
    [Fact]
    public void GitIgnoreMatcher_HandlesPatternsAndNegationsWithoutAGitRepository()
    {
        var matcher = new GitIgnoreMatcher(rules:
        [
            "*.log",
            "!keep.log",
            "cache/"
        ]);

        Assert.True(matcher.IsIgnored("build/output.log", isDirectory: false));
        Assert.False(matcher.IsIgnored("keep.log", isDirectory: false));
        Assert.True(matcher.IsIgnored("cache", isDirectory: true));
        Assert.False(matcher.IsIgnored("src/main.cs", isDirectory: false));
    }

    [Fact]
    public void GitIgnoreMatcher_AppliesNestedRulesRelativeToTheirDirectory()
    {
        var matcher = new GitIgnoreMatcher(
        [
            new IgnoreRuleSet(basePath: string.Empty, rules: ["*.log"]),
            new IgnoreRuleSet(basePath: "src", rules: ["!keep.log", "*.tmp"])
        ]);

        Assert.True(matcher.IsIgnored("build/output.log", isDirectory: false));
        Assert.False(matcher.IsIgnored("src/keep.log", isDirectory: false));
        Assert.True(matcher.IsIgnored("src/generated.tmp", isDirectory: false));
        Assert.False(matcher.IsIgnored("tests/generated.tmp", isDirectory: false));
    }

    [Fact]
    public void GitIgnoreMatcher_DoesNotOverrideAnInheritedMatchWhenNestedRulesDoNotMatch()
    {
        var matcher = new GitIgnoreMatcher(
        [
            new IgnoreRuleSet(basePath: string.Empty, rules: ["*.log"]),
            new IgnoreRuleSet(basePath: "src", rules: ["*.tmp"])
        ]);

        Assert.True(matcher.IsIgnored("src/output.log", isDirectory: false));
    }

    [Fact]
    public void GitIgnoreMatcher_DelegatesCommentsEmptyRulesAndEscapedPrefixesToIgnore()
    {
        var matcher = new GitIgnoreMatcher(
        [
            "",
            "# a comment",
            @"\!important",
            @"\#notes"
        ]);

        Assert.True(matcher.IsIgnored("!important", isDirectory: false));
        Assert.True(matcher.IsIgnored("#notes", isDirectory: false));
        Assert.False(matcher.IsIgnored("ordinary.txt", isDirectory: false));
    }
}
