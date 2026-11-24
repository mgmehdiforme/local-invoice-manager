using LocalInvoiceManager.Application.Documents;

namespace Domain.Tests;

public class NumberToWordsTests
{
    [Theory]
    [InlineData(0, "صفر")]
    [InlineData(5, "پنج")]
    [InlineData(15, "پانزده")]
    [InlineData(21, "بیست و یک")]
    [InlineData(100, "یکصد")]
    [InlineData(1250000, "یک میلیون و دویست و پنجاه هزار")]
    [InlineData(2205000, "دو میلیون و دویست و پنج هزار")]
    [InlineData(1_000_000_000, "یک میلیارد")]
    public void Converts(long n, string expected) => Assert.Equal(expected, NumberToWords.Convert(n));

    [Theory]
    [InlineData(0, "Zero")]
    [InlineData(21, "Twenty-one")]
    [InlineData(1234, "One thousand two hundred thirty-four")]
    [InlineData(1_000_005, "One million five")]
    public void Converts_english(long n, string expected) => Assert.Equal(expected, NumberToWords.English(n));
}
