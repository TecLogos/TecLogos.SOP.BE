using TecLogos.SOP.EnumsAndConstants;

namespace TecLogos.SOP.WebModel.SOP
{
    public class SopFilterRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
        public SopStatus? Status { get; set; }
    }
}
