using DataService.Memory;
using Microsoft.AspNetCore.Http;
using SearchEntities;
using SharedEntities;
using ZavaDatabaseInitialization;
using ZavaWorkingModes;

namespace DataService.Endpoints;

public static class ProductAiActions
{
    public static async Task<IResult> AISearch(string search, Context db, MemoryContext mc,
        WorkingMode workingMode = WorkingMode.MafLocal)
    {
        workingMode = WorkingMode.MafOllama;

        var result = await mc.Search(search, db, workingMode);
        return Results.Ok(result);
    }
}
