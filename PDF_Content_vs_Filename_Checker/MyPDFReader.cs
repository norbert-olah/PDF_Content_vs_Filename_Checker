using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDF_Content_vs_Filename_Checker
{


    public static class iText_GetPdfText
    {
        /// <summary>
        /// Extracts text from PDF file. (only selectable text content from the pages, not OCR from images)
        /// </summary>
        /// <param name="filepath">input file path</param>
        /// <param name="zone">Rectangle which specifies the zone where the text is extracted from a page. if it's null, then the full page is processed.</param>
        /// <param name="pages">List of pages to extract data from. If null or first item is 0, all pages will be extracted.</param>
        /// <returns>a list of strings. one string from each page</returns>
        public static List<string> GetPdfTextFromPages(string filepath, RectangleF? zone = null, List<int> pages = null)
        {
            using (PdfReader reader = new PdfReader(filepath))
            {
                List<string> result = new List<string>();

                if (pages == null || pages.First() == 0) //then read all pages
                {
                    pages = Enumerable.Range(1, reader.NumberOfPages).ToList(); //create the list of all pagenumbers in the actual PDF
                }

                foreach (var i in pages)
                {
                    if (i > reader.NumberOfPages)
                    {
                        continue;
                    }

                    if (zone.HasValue)
                    { //zone based text extract
                        float x = Utilities.MillimetersToPoints(zone.Value.X);
                        float y = Utilities.MillimetersToPoints(zone.Value.Y);
                        float w = Utilities.MillimetersToPoints(zone.Value.Width);
                        float h = Utilities.MillimetersToPoints(zone.Value.Height);

                        var pagesize = reader.GetPageSizeWithRotation(i);
                        iTextSharp.text.Rectangle rect = new iTextSharp.text.Rectangle(x, pagesize.Top - y, x + w, pagesize.Top - y - h); //tanslate coordinates to iText 

                        RenderFilter[] renderFilter = new RenderFilter[1];
                        renderFilter[0] = new RegionTextRenderFilter(rect);
                        ITextExtractionStrategy textExtractionStrategy = new FilteredTextRenderListener(new LocationTextExtractionStrategy(), renderFilter);
                        string text = PdfTextExtractor.GetTextFromPage(reader, i, textExtractionStrategy);
                        result.Add(text.Replace("\n", Environment.NewLine));
                    }
                    else
                    { //full page text extract
                        string text = PdfTextExtractor.GetTextFromPage(reader, i);
                        result.Add(text.Replace("\n", Environment.NewLine));
                    }
                }



                reader.Close();
                return result;
            }
        }
    }
}
