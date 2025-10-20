using Xunit;
using Ason.Client.Execution;

namespace Ason.Tests.Security;

public class KeywordScriptValidatorTests {
    [Fact]
    public void EmptyScript_ReturnsError() {
        var v = new KeywordScriptValidator(new[]{"Forbidden"});
        var result = v.Validate("  \n \t");
        Assert.Equal("Empty script", result);
    }

    [Fact]
    public void ForbiddenKeyword_ReturnsMessage() {
        var v = new KeywordScriptValidator(new[]{"System.Reflection","Assembly.Load"});
        var script = "// test\nvar t = System.Reflection.Assembly.GetExecutingAssembly();";
        var result = v.Validate(script);
        Assert.NotNull(result);
        Assert.Contains("System.Reflection", result);
    }


    [Fact]
    public void CleanScript_ReturnsNull() {
        var v = new KeywordScriptValidator(new[]{"System.Reflection"});
        var script = "return 123;";
        var result = v.Validate(script);
        Assert.Null(result);
    }

    [Fact]
    public void CaseSensitive_Check() {
        var v = new KeywordScriptValidator(new[]{"System.Reflection"});
        var script = "system.reflection should not match due to case";
        var result = v.Validate(script);
        Assert.Null(result); // Contains uses Ordinal - case sensitive
    }
}
