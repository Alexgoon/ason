using Xunit;
using Ason;

namespace Ason.Tests.ScriptHelpers;

public class ScriptReplyProcessorTests {
    [Fact]
    public void Removes_CodeFences_And_Comments() {
        string input = "```csharp\n// comment\nreturn 5; // trailing\n```";
        var processed = ScriptReplyProcessor.Process(input);
        Assert.DoesNotContain("```", processed);
        Assert.DoesNotContain("comment", processed);
        Assert.Equal("return 5;", processed.Trim());
    }



    [Fact]
    public void Duplicate_Usings_Removed() {
        string input = "using System;\nusing System;\nreturn 9;";
        var processed = ScriptReplyProcessor.Process(input);
        int count = processed.Split('\n').Count(l => l.Trim() == "using System;");
        Assert.True(count <= 1);
    }
}
