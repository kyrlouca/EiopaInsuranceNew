using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace ExcelCreatorV
{
    public class WorkbookStyles
    {
        public XSSFWorkbook DestExcelTemplateBook { get; }
        public ICellStyle BasicBorderStyle { get; }
        public ICellStyle FullBorderStyle { get; }
        public ICellStyle RowLabelStyle { get; }
        public ICellStyle TitleH2Style { get; }
        public ICellStyle BasicItalicStyle { get; }
        public ICellStyle DateStyle { get; }
        public ICellStyle RealStyle { get; }
        public ICellStyle PercentStyle { get; }
        public ICellStyle IntStyle { get; }
        public ICellStyle TextStyle { get; }
        public ICellStyle ColumnLabelStyle { get; }
        public ICellStyle ColumnOpenLabelStyle { get; }
        public ICellStyle TileStyle { get; }
        public ICellStyle HyperStyle { get; }
        public ICellStyle ShadedStyle { get; }
        public ICellStyle NullStyle { get; }



        public WorkbookStyles(XSSFWorkbook workBook)
        {
            DestExcelTemplateBook = workBook;
            BasicBorderStyle = SetBasicBorderStyle();
            FullBorderStyle = SetFullBorderStyle();
            RowLabelStyle = SetRowLabelStyle();
            TitleH2Style = SetTitleH2Style();
            BasicItalicStyle = SetBasicItalicStyle();
            DateStyle = SetDateStyle();
            RealStyle = SetRealStyle();
            PercentStyle = SetPercentStyle();
            IntStyle = SetIntStyle();
            TextStyle = SetTextStyle();
            ColumnLabelStyle = SetColumnLabelStyle();
            ColumnOpenLabelStyle = SetColumnLabelOpenStyle();
            TileStyle = SetTitleStyle();
            HyperStyle = SetHyperStyle();
            ShadedStyle = SetShadedStyle();
            NullStyle = SetNullStyle();

        }

        private ICellStyle SetBasicBorderStyle()
        {
            var basicBorderStyle = DestExcelTemplateBook.CreateCellStyle();
            basicBorderStyle.BorderBottom = BorderStyle.Thin;
            basicBorderStyle.BorderRight = BorderStyle.Thin;
            return basicBorderStyle;
        }


        private ICellStyle SetFullBorderStyle()
        {
            var fullBorderStyle = DestExcelTemplateBook.CreateCellStyle();
            fullBorderStyle.BorderTop = BorderStyle.Thin;
            fullBorderStyle.BorderBottom = BorderStyle.Thin;
            fullBorderStyle.BorderRight = BorderStyle.Thin;
            fullBorderStyle.BorderDiagonalLineStyle = BorderStyle.None;
            return fullBorderStyle;
        }

        private ICellStyle SetTitleH2Style()
        {
            var value = DestExcelTemplateBook.CreateCellStyle();
            var boldFont = DestExcelTemplateBook.CreateFont();
            boldFont.FontHeightInPoints = 12;
            boldFont.FontName = "Calibri";
            boldFont.IsBold = true;
            value.SetFont(boldFont);
            return value;
        }


        private ICellStyle SetBasicItalicStyle()
        {
            var basicItalic = DestExcelTemplateBook.CreateCellStyle();
            var italicFont = DestExcelTemplateBook.CreateFont();
            italicFont.FontHeightInPoints = 12;
            italicFont.FontName = "Calibri";
            italicFont.IsBold = false;
            italicFont.IsItalic = true;

            basicItalic.SetFont(italicFont);
            return basicItalic;
        }

        private ICellStyle SetDateStyle()
        {
            var dateStyle = DestExcelTemplateBook.CreateCellStyle();
            dateStyle.CloneStyleFrom(BasicBorderStyle);
            var dataFormatCustom = DestExcelTemplateBook.CreateDataFormat();
            dateStyle.DataFormat = dataFormatCustom.GetFormat("yyyy-MM-dd");
            dateStyle.Alignment = HorizontalAlignment.Center;
            return dateStyle;
        }

        private ICellStyle SetRealStyle()
        {
            var realStyle = DestExcelTemplateBook.CreateCellStyle();
            realStyle.CloneStyleFrom(BasicBorderStyle);
            var dataNumberFormat = DestExcelTemplateBook.CreateDataFormat();
            realStyle.DataFormat = dataNumberFormat.GetFormat("#,##0.00");
            realStyle.Alignment = HorizontalAlignment.Right;
            return realStyle;
        }

        private ICellStyle SetPercentStyle()
        {
            var percentStyle = DestExcelTemplateBook.CreateCellStyle();
            percentStyle.CloneStyleFrom(BasicBorderStyle);
            var dataPercentFormat = DestExcelTemplateBook.CreateDataFormat();
            percentStyle.DataFormat = dataPercentFormat.GetFormat("0.00%");
            percentStyle.Alignment = HorizontalAlignment.Right;
            return percentStyle;
        }

        private ICellStyle SetIntStyle()
        {
            var intStyle = DestExcelTemplateBook.CreateCellStyle();
            intStyle.CloneStyleFrom(BasicBorderStyle);
            var dataIntFormat = DestExcelTemplateBook.CreateDataFormat();
            intStyle.DataFormat = dataIntFormat.GetFormat("#,##");
            intStyle.Alignment = HorizontalAlignment.Right;
            return intStyle;
        }

        private ICellStyle SetTextStyle()
        {
            var textStyle = DestExcelTemplateBook.CreateCellStyle();
            textStyle.CloneStyleFrom(BasicBorderStyle);
            textStyle.Alignment = HorizontalAlignment.Left;
            return textStyle;

        }
        private ICellStyle SetColumnLabelStyle()
        {
            var columnLabelStyle = DestExcelTemplateBook.CreateCellStyle();
            columnLabelStyle.BorderBottom = BorderStyle.Thick;
            columnLabelStyle.BorderTop = BorderStyle.Thick;
            columnLabelStyle.BorderRight = BorderStyle.Thin;
            columnLabelStyle.BorderLeft = BorderStyle.Thin;
            columnLabelStyle.Alignment = HorizontalAlignment.Center;
            return columnLabelStyle;
        }
        private ICellStyle SetColumnLabelOpenStyle()
        {
            var columnLabelOpenStyle = DestExcelTemplateBook.CreateCellStyle();
            columnLabelOpenStyle.BorderTop = BorderStyle.Thick;
            columnLabelOpenStyle.BorderRight = BorderStyle.Thin;
            columnLabelOpenStyle.WrapText = true;
            columnLabelOpenStyle.Alignment = HorizontalAlignment.Center;
            return columnLabelOpenStyle;
        }

        private ICellStyle SetTitleStyle()
        {

            var titleStyle = DestExcelTemplateBook.CreateCellStyle();

            var titleFont = DestExcelTemplateBook.CreateFont();
            titleFont.FontHeightInPoints = 12;
            titleFont.FontName = "Calibri";
            titleFont.IsBold = true;
            //titleFont.Color = NPOI.HSSF.Util.HSSFColor.Red.Index;            
            titleStyle.SetFont(titleFont);
            return titleStyle;
        }
        private ICellStyle SetHyperStyle()
        {
            var hyperFont = DestExcelTemplateBook.CreateFont();
            hyperFont.FontName = "Calibri";
            hyperFont.FontHeightInPoints = 11;
            hyperFont.IsBold = true;
            hyperFont.Underline = FontUnderlineType.Single;
            hyperFont.Color = NPOI.HSSF.Util.HSSFColor.Red.Index;

            var hyperStyle = DestExcelTemplateBook.CreateCellStyle();
            hyperStyle.SetFont(hyperFont);
            return hyperStyle;
        }

        private ICellStyle SetShadedStyle()
        {
            var shadedFont = DestExcelTemplateBook.CreateFont();            
            shadedFont.Color = NPOI.HSSF.Util.HSSFColor.Grey40Percent.Index; //to avoid showing the text updated as @


            var shadedStyle = DestExcelTemplateBook.CreateCellStyle();
            shadedStyle.CloneStyleFrom(BasicBorderStyle);
            shadedStyle.FillForegroundColor = NPOI.HSSF.Util.HSSFColor.Grey40Percent.Index;
            shadedStyle.FillBackgroundColor = NPOI.HSSF.Util.HSSFColor.Grey40Percent.Index;
            shadedStyle.FillPattern = FillPattern.SolidForeground;
            shadedStyle.BorderDiagonal = BorderDiagonal.Both;
            shadedStyle.BorderDiagonalColor = NPOI.HSSF.Util.HSSFColor.Red.Index;
            shadedStyle.SetFont(shadedFont);
            return shadedStyle;
        }


        private ICellStyle SetRowLabelStyle()
        {
            var theStyle = DestExcelTemplateBook.CreateCellStyle();
            theStyle.BorderBottom = BorderStyle.Thin;
            theStyle.BorderRight = BorderStyle.Thick;
            return theStyle;
        }


        private ICellStyle SetNullStyle()
        {
            var nullStyle = DestExcelTemplateBook.CreateCellStyle();
            nullStyle.CloneStyleFrom(BasicBorderStyle);
            nullStyle.FillBackgroundColor = NPOI.HSSF.Util.HSSFColor.Yellow.Index;
            return nullStyle;
        }
    }
}