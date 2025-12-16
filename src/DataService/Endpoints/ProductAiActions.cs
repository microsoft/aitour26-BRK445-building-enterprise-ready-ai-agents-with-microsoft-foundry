using DataService.Memory;
using SharedEntities;
using SearchEntities;
using Microsoft.AspNetCore.Http;
using ZavaDatabaseInitialization;

namespace DataService.Endpoints;

public static class ProductAiActions
{
    public static async Task<IResult> AISearch(string search, Context db, MemoryContext mc)
    {
        var result = await mc.Search(search, db);
        return Results.Ok(result);
    }
}
