using Windows.Media.Capture;
using Windows.Media.Capture.Frames;

namespace Itp.Handheld.WpfClient.Capture.WinRt;

public sealed record WinRtCameraDeviceInfo(string FriendlyName, string Id);

internal static class WinRtCameraDeviceEnumerator
{
    public static async Task<IReadOnlyList<WinRtCameraDeviceInfo>> EnumerateAsync()
    {
        var groups = await MediaFrameSourceGroup.FindAllAsync();
        return groups
            .Where(g => g.SourceInfos.Any(IsColorVideoSource))
            .Select(g => new WinRtCameraDeviceInfo(g.DisplayName, g.Id))
            .ToList();
    }

    // Depth and infrared sources share the group; only colour streams can be previewed.
    internal static bool IsColorVideoSource(MediaFrameSourceInfo info)
        => info.SourceKind == MediaFrameSourceKind.Color
        && (info.MediaStreamType == MediaStreamType.VideoPreview
            || info.MediaStreamType == MediaStreamType.VideoRecord);
}
