using System.Runtime.CompilerServices;

// Lets LoomaCloud.Infrastructure.VectorStore.Qdrant.Tests directly unit-test
// small internal, I/O-free logic pieces (e.g. AnswerCacheEviction,
// AnswerCacheStaleness) without making them part of this assembly's public
// surface — same pattern already used for LoomaCloud.Application ->
// LoomaCloud.Application.Tests.
[assembly: InternalsVisibleTo("LoomaCloud.Infrastructure.VectorStore.Qdrant.Tests")]
