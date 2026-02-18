using KuechenRezepte.Helpers;

namespace KuechenRezepte.Tests;

public class StringHelperTests
{
    [Fact]
    public void TrimToLength_NullInput_ReturnsNull()
    {
        var result = StringHelper.TrimToLength(null, 10);
        Assert.Null(result);
    }

    [Fact]
    public void TrimToLength_WhitespaceOnly_ReturnsNull()
    {
        var result = StringHelper.TrimToLength("   ", 10);
        Assert.Null(result);
    }

    [Fact]
    public void TrimToLength_ShortString_ReturnsTrimmmedString()
    {
        var result = StringHelper.TrimToLength("  hello  ", 20);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void TrimToLength_LongString_TruncatesAfterTrimming()
    {
        var result = StringHelper.TrimToLength("  hello world  ", 5);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void TrimToLength_ExactLength_ReturnsExactString()
    {
        var result = StringHelper.TrimToLength("abc", 3);
        Assert.Equal("abc", result);
    }

    [Fact]
    public void TrimToLength_EmptyString_ReturnsNull()
    {
        var result = StringHelper.TrimToLength("", 10);
        Assert.Null(result);
    }
}
