using System.Collections.Generic;

namespace HVACLoadTerminals.CreateParameters;

public class ReportItem
{
    public string CreatorName { get; set; }
    public string ParametersSummary { get; set; }
    public List<string> Parameters { get; set; }
    public List<string> Categories { get; set; }
}