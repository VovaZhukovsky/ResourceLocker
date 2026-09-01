using ResourceLocker.Core;

namespace ResourceLocker.Core.Tests;

public class CommandParserTests
{
    [Fact]
    public void Validate_Set_Command_With_Three_Args_Should_Be_Valid()
    {
        var result = CommandParser.Parse("set resource:1 data");
        Assert.True(result.Command is "set");
        Assert.True(result.Key is "resource:1");
        Assert.True(result.Value is "data");
    }

    [Fact]
    public void Validate_Get_Command_With_Two_Args_Should_Be_Valid()
    {
        var result = CommandParser.Parse("get resource:1");
        Assert.True(result.Command is "get");
        Assert.True(result.Key is "resource:1");
        Assert.True(result.Value.IsEmpty);
    }

    [Fact]
    public void Validate_Incorrect_Command_Should_Be_Empty_Result()
    {
        var result = CommandParser.Parse("invalidCommand");
        Assert.True(result.Command.IsEmpty);
        Assert.True(result.Key.IsEmpty);
        Assert.True(result.Value.IsEmpty);
    }

    [Fact]
    public void Validate_Command_With_Extra_Spaces_Should_Be_Valid()
    {
        var result = CommandParser.Parse("set  resource:1  data");
        Assert.True(result.Command is "set");
        Assert.True(result.Key is "resource:1");
        Assert.True(result.Value is "data");
    }
}
