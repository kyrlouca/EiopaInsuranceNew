namespace ExcelCreatorV
{
    //S.02.02.01-S.02.02.01.01,S.02.02.01.02
    //S.04.01.01-S.04.01.01.01,S.04.01.01.02,S.04.01.01.03,S.04.01.01.04
    //S.19.01.21-S.19.01.21.01,S.19.01.21.02,S.19.01.21.03,S.19.01.21.04
    //S.22.06.01-S.22.06.01.01,S.22.06.01.02 +S.22.06.01.03,S.22.06.01.04


    internal static class SpecialTemplates
    {

        public static List<SpecialHorizontalTemplate> Records { get; }
        static SpecialTemplates()
        {
            Records = new()
            {
                new SpecialHorizontalTemplate("S.02.02.01", "S.02.02.01", new[] { new string[] { "S.02.02.01.01", "S.02.02.01.02" } }),
                new SpecialHorizontalTemplate("S.04.01.01", "S.04.01.01", new[] { new string[] { "S.04.01.01.01", "S.04.01.01.02", "S.04.01.01.03", "S.04.01.01.04" } }),
                new SpecialHorizontalTemplate("S.05.02.01", "S.05.02.01", new[] { new string[] { "S.05.02.01.01", "S.05.02.01.02", "S.05.02.01.03" }, new string[] { "S.05.02.01.04", "S.05.02.01.05", "S.05.02.01.06" } }),
                new SpecialHorizontalTemplate("S.19.01.01", "S.19.01.01", new[] {
                    new string[] { "S.19.01.01.01", "S.19.01.01.02", "S.19.01.01.03", "S.19.01.01.04", "S.19.01.01.05" ,"S.19.01.01.06" },
                    new string[] { "S.19.01.01.07", "S.19.01.01.08", "S.19.01.01.09", "S.19.01.01.10", "S.19.01.01.11" ,"S.19.01.01.12" },
                    new string[] { "S.19.01.01.13", "S.19.01.01.14", "S.19.01.01.15", "S.19.01.01.16", "S.19.01.01.17" ,"S.19.01.01.18" },
                    new string[] { "S.19.01.01.19" },
                    new string[] { "S.19.01.01.20" },
                    new string[] { "S.19.01.01.21" },
                }),
                new SpecialHorizontalTemplate("S.19.01.21", "S.19.01.21", new[] { new string[] { "S.19.01.21.01", "S.19.01.21.02" , "S.19.01.21.03" , "S.19.01.21.04" } }),
                new SpecialHorizontalTemplate("S.22.06.01", "S.22.06.01", new[]
                    {
                        new string[] { "S.22.06.01.01", "S.22.06.01.01" },
                        new string[] { "S.22.06.01.03", "S.22.06.01.04" }
                })
            };
        }
    }

    public class SpecialHorizontalTemplate
    {
        public string TemplateCode { get; init; }
        public string TemplateSheetName { get; init; }
        public String[][] TableCodesArray { get; init; }
        public List<List<string>> TableCodes { get; init; }
        public SpecialHorizontalTemplate(string templateCode, string templateSheetName, string[][] tableCodes)
        {
            TemplateCode = templateCode;
            TemplateSheetName = templateSheetName;
            TableCodesArray = tableCodes;
            TableCodes = TableCodesArray.Select(tc => tc.ToList()).ToList();
        }

    }
}