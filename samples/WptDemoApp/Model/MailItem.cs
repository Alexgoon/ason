using Ason;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfSampleApp.Model;

[AsonModel]
public class MailItem {
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public List<string> To { get; set; } = new List<string>();
    public DateTime ReceivedDate { get; set; }
}
