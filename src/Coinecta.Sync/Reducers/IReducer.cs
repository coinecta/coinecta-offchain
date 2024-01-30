using PallasDotnet.Models;

namespace Coinecta.Sync.Reducers;

public interface IReducer
{
    Task RollForwardAsync(NextResponse response);
    Task RollBackwardAsync(NextResponse response);
}