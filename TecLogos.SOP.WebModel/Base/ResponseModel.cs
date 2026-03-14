using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecLogos.SOP.WebModel.Base
{
    public class ResponseModel
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? ID { get; set; }
    }

}
