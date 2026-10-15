using LoomaCloud.Application.Internal;
using Xunit;

namespace LoomaCloud.Application.Tests;

public class SmallTalkDetectorTests
{
    [Theory]
    [InlineData("Hi Looma")]
    [InlineData("hello")]
    [InlineData("Hey, Looma!")]
    [InlineData("Здравей Лума")]
    [InlineData("Здравей, Лума!")]
    [InlineData("Добър ден")]
    [InlineData("Благодаря, Лума")]
    [InlineData("Thanks Looma")]
    [InlineData("Good morning Looma")]
    [InlineData("How are you?")]
    [InlineData("Как си?")]
    [InlineData("Лума")]
    [InlineData("Bonjour Looma")]
    [InlineData("Merci beaucoup")]
    [InlineData("Comment allez-vous ?")]
    [InlineData("Hola, ¿qué tal?")]
    [InlineData("Buenos días")]
    [InlineData("Hallo Looma")]
    [InlineData("Guten Tag")]
    [InlineData("Wie geht's?")]
    [InlineData("Ciao Looma")]
    [InlineData("Come stai?")]
    [InlineData("Olá Looma")]
    [InlineData("Bom dia")]
    [InlineData("Привет, Лума!")]
    [InlineData("Как дела?")]
    [InlineData("Dzień dobry")]
    [InlineData("Merhaba Looma")]
    [InlineData("Hoi Looma")]
    public void IsSmallTalk_GreetingsAndThanks_ReturnsTrue(string message)
    {
        Assert.True(SmallTalkDetector.IsSmallTalk(message));
    }

    [Theory]
    [InlineData("Bonjour Looma")]
    [InlineData("こんにちは")]
    [InlineData("Hola, ¿qué tal?")]
    public void MightBeSmallTalk_ShortMessageInAnyLanguage_ReturnsTrue(string message)
    {
        Assert.True(SmallTalkDetector.MightBeSmallTalk(message));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Please summarize the whole quarterly report in detail for me")]
    public void MightBeSmallTalk_EmptyOrLongMessage_ReturnsFalse(string message)
    {
        Assert.False(SmallTalkDetector.MightBeSmallTalk(message));
    }

    [Theory]
    [InlineData("Hi Looma, what does the contract say about termination?")]
    [InlineData("What is Looma?")]
    [InlineData("Summarize the attached document")]
    [InlineData("Кажи ми какво пише в договора")]
    [InlineData("you")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSmallTalk_RealQuestionsOrEmpty_ReturnsFalse(string message)
    {
        Assert.False(SmallTalkDetector.IsSmallTalk(message));
    }
}
