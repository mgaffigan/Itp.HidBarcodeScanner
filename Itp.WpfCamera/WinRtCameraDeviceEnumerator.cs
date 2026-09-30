using Windows.Media.Capture;
using Windows.Media.Capture.Frames;

namespace Itp.WpfCamera;

/// <summary>
/// Finds cameras that can be previewed.
/// </summary>
public static class WinRtCameraDeviceEnumerator
{
    /// <summary>
    /// Lists the cameras with a color video source, for <see cref="WinRtCamera.OpenAsync"/>.
    /// </summary>
    public static async Task<IReadOnlyList<MediaFrameSourceGroup>> EnumerateAsync()
    {
        var groups = await MediaFrameSourceGroup.FindAllAsync();
        return groups.Where(g => g.SourceInfos.Any(IsColorVideoSource)).ToList();
    }

    // Depth and infrared sources share the group; only colour streams can be previewed.
    internal static bool IsColorVideoSource(MediaFrameSourceInfo info)
        => info.SourceKind == MediaFrameSourceKind.Color
        && (info.MediaStreamType == MediaStreamType.VideoPreview
            || info.MediaStreamType == MediaStreamType.VideoRecord);
}
