namespace HojaDeRuta.Models.Config
{
    public class UploadSettings
    {
        public int MaxFileSizeMb { get; set; } = 60;

        public long GetMaxFileSizeBytes()
        {
            return MaxFileSizeMb * 1024L * 1024L;
        }

        public string GetMaxFileSizeLabel()
        {
            return $"{MaxFileSizeMb} MB";
        }
    }
}
