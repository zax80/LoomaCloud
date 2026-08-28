using LoomaCloud.Infrastructure.VectorStore.Qdrant;
using Xunit;

namespace LoomaCloud.Infrastructure.VectorStore.Qdrant.Tests;

public class AnswerCacheQuestionNormalizerTests
{
    [Theory]
    [InlineData("What vector database does LoomaCloud use?", "what vector database does loomacloud use?")]
    [InlineData("  What vector database does LoomaCloud use?  ", "what vector database does loomacloud use?")]
    [InlineData("What   vector\tdatabase does LoomaCloud use?", "what vector database does loomacloud use?")]
    [InlineData("WHAT VECTOR DATABASE DOES LOOMACLOUD USE?", "what vector database does loomacloud use?")]
    public void Normalize_TreatsCaseAndWhitespaceVariantsAsTheSameQuestion(string input, string expected)
    {
        Assert.Equal(expected, AnswerCacheQuestionNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_DifferentQuestions_ProduceDifferentKeys()
    {
        var a = AnswerCacheQuestionNormalizer.Normalize("What vector database does LoomaCloud use?");
        var b = AnswerCacheQuestionNormalizer.Normalize("Why does LoomaCloud use only one vector database?");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Normalize_EmptyOrWhitespace_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, AnswerCacheQuestionNormalizer.Normalize("   "));
        Assert.Equal(string.Empty, AnswerCacheQuestionNormalizer.Normalize(string.Empty));
    }

    [Fact]
    public void Normalize_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AnswerCacheQuestionNormalizer.Normalize(null!));
    }
}
