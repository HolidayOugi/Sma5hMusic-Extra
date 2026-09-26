namespace Sma5hMusic.GUI.Interfaces
{
    public interface ISeriesIconService
    {
        string GetIconPath(string uiSeriesId);
        string GetIconPreviewPath(string uiSeriesId);
        string CreatePreviewFromBntx(string uiSeriesId);
        string CreatePreviewFromFile(string iconPath);
        string SaveIcon(string sourcePngPath, string uiSeriesId);
    }
}
