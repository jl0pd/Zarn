using Grpc.Core;
using Benchmarks.Grpc;
using G = Benchmarks.Grpc;

namespace Benchmarks.Services;

public class GrpcCalculatorService : G.Calculator.CalculatorBase
{
    public override Task<BinaryOperationResult> Add(BinaryOperationRequest request, ServerCallContext context)
    {
        return Task.FromResult(new BinaryOperationResult
        {
            Result = request.Left + request.Right,
        });
    }

    public override async Task AddStreaming(IAsyncStreamReader<BinaryOperationRequest> requestStream, IServerStreamWriter<BinaryOperationResult> responseStream, ServerCallContext context)
    {
        await foreach (var request in requestStream.ReadAllAsync())
        {
            await responseStream.WriteAsync(new BinaryOperationResult
            {
                Result = request.Left + request.Right,
            });
        }
    }
}
