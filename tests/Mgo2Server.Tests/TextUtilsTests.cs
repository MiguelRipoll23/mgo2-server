using Mgo2Server.Shared.Utils;

namespace Mgo2Server.Tests;

/// <summary>
/// Pins the line wrapping the server applies to the text it authors itself.
/// <para>
/// The client wraps none of its own rendering, so a line longer than the width of
/// the screen's container is cut off; the server must break it. A break is taken
/// at a space where one fits so a word is not split, and at the width itself when
/// a single word does not.
/// </para>
/// </summary>
[Trait("Category", "Shared")]
public sealed class TextUtilsTests
{
    [Fact]
    public void A_line_that_already_fits_is_returned_untouched()
    {
        Assert.Equal("hello", TextUtils.Wrap("hello", 10));
    }

    [Fact]
    public void A_long_line_is_broken_at_a_space_that_fits()
    {
        Assert.Equal("the quick\nbrown fox\njumps", TextUtils.Wrap("the quick brown fox jumps", 10));
    }

    [Fact]
    public void A_word_wider_than_the_width_is_broken_at_the_width()
    {
        Assert.Equal("abcde\nfghij\nkl", TextUtils.Wrap("abcdefghijkl", 5));
    }

    [Fact]
    public void Line_breaks_already_in_the_text_are_kept()
    {
        Assert.Equal("one\ntwo\nthree\nfour", TextUtils.Wrap("one\ntwo three four", 7));
    }

    [Fact]
    public void Every_line_comes_out_no_longer_than_the_width()
    {
        var wrapped = TextUtils.Wrap("a bb ccc dddd eeeee ffffff ggggggg", 6);

        Assert.All(wrapped.Split('\n'), line => Assert.True(line.Length <= 6, line));
    }

    [Fact]
    public void A_carriage_return_pair_reads_as_one_line_break()
    {
        Assert.Equal("a\nb", TextUtils.Wrap("a\r\nb", 5));
    }

    [Fact]
    public void An_empty_text_stays_empty()
    {
        Assert.Equal(string.Empty, TextUtils.Wrap(string.Empty, 10));
    }

    [Fact]
    public void A_width_below_one_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextUtils.Wrap("text", 0));
    }
}
