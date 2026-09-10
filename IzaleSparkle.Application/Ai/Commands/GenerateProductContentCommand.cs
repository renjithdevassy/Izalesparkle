using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Application.Ai.Commands;

// ── GENERATE PRODUCT CONTENT ─────────────────────────────────
public record GenerateProductContentCommand(GenerateProductContentRequest Request)
    : IRequest<GeneratedProductContent>;

public class GenerateProductContentHandler(IAiContentService ai)
    : IRequestHandler<GenerateProductContentCommand, GeneratedProductContent>
{
    public Task<GeneratedProductContent> Handle(
        GenerateProductContentCommand cmd, CancellationToken ct)
        => ai.GenerateProductContentAsync(cmd.Request, ct);
}

// ── LIST AVAILABLE MODELS ────────────────────────────────────
public record ListAiModelsQuery : IRequest<IReadOnlyList<AiModelInfo>>;

public class ListAiModelsHandler(IAiContentService ai)
    : IRequestHandler<ListAiModelsQuery, IReadOnlyList<AiModelInfo>>
{
    public Task<IReadOnlyList<AiModelInfo>> Handle(ListAiModelsQuery q, CancellationToken ct)
        => ai.ListModelsAsync(ct);
}
