using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace CorchEdges;

internal sealed class GraphFacade : IGraphFacade
{
    private readonly GraphServiceClient _g;
    public GraphFacade(GraphServiceClient g) => _g = g;
    public Task<ListItem?> GetListItemAsync(string site, string list, string itm) =>
        _g.Sites[site].Lists[list].Items[itm].GetAsync(o => o.QueryParameters.Expand = ["fields"]);
    public Task<DriveItem?> GetDriveItemAsync(string site, string list, string itm) =>
        _g.Sites[site].Lists[list].Items[itm].DriveItem.GetAsync();
    public async Task<Stream> DownloadAsync(string driveId, string itemId) =>
        await _g.Drives[driveId].Items[itemId].Content.GetAsync() ?? throw new InvalidOperationException("null stream");
}