using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Threading;

namespace PDF_Content_vs_Filename_Checker
{
    class Program
    {

        /* Norbert Olah, 2020-11-24
         * extract text from PDF and comapre with filename using regex patterns
         *
         * 
         * prupose of the app: there is a misterious error in our DocComp sw, where IDs are mixed in certain situations. 
         * this program is intended to check all the PDFs and extract text in a specified position and based on a regex patter
         * these IDs then compared to the ID in the filename and a CSV report is generated.
         * 
         * open sourced beacuse of iText
         * 
         * other comments and printouts may be in hungarian.
         * 
         */


        static void Main(string[] args)
        {

            if (args.Length < 2)
            {
                Console.WriteLine("A program új verziójában 2 kötelező paraméter van: 1: beállítások XML fájlja és 2: a keresési feltétel a fájlokra.");
                Console.WriteLine(@"Keresési feltétel lehet minta, pl.: c:\teszt\*.pdf  vagy konkrét fájl, c:\teszt\akármi.pdf");
                Console.WriteLine("A program egy eredmeny.csv fájlba fogja írni a találatokat.");

                if (Debugger.IsAttached)
                {
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                }
                return;
            }

       

            Options opts = Options.CreateFromXml(args[0]);
            Console.WriteLine($"Beállítások betöltve: {args[0]}");


            //tidy up page list
            if (opts.PageList == null || opts.PageList.Count < 1)
            {
                opts.PageList.Add(0);
            }
            opts.PageList.RemoveAll(x => x < 0);
            opts.PageList.Sort();


            string dir = Path.GetDirectoryName(args[1]);
            if (!Path.IsPathRooted(args[1]))
            {
                dir = Path.GetFullPath(dir);
            }

            string filemask = Path.GetFileName(args[1]);

            List<string> files = Directory.GetFiles(dir, filemask, SearchOption.TopDirectoryOnly).ToList();



            //int n = 0;
            int i = 0;
            int count = files.Count();
            object lock_obj = new object();

            ConcurrentBag<string> sor = new ConcurrentBag<string>();
            Console.WriteLine("Párhuzamos feldolgozás indul...");
            Parallel.ForEach(files, filename =>
            {
                string ezmegylogba = ProcessFile(filename, opts.SearchRegion, opts.FileNameSearchPattern, opts.PdfContentSearchPattern, opts.PdfContentSearchPattern2, opts.PageList);
                sor.Add(ezmegylogba);

                //Interlocked.Increment(ref i);
                lock (lock_obj)
                {
                    i++;
                    //Console.Clear();
                    Console.SetCursorPosition(0, 0);
                    Console.Write($"{i} / {count} fájl feldolgozva.                              ");
                }
            });
            Console.Clear();
            Console.WriteLine($"{i} / {count} fájl feldolgozva.                              ");
            Console.WriteLine("Párhuzamos feldolgozás lefuttott. Eredménylista rendezése...");
            var eredmeny = new List<string>();
            // a párhuzamosság és sorbarendezés miatt a fejlécet csak a végén csinálhatjuk meg, hogy beírjuk a fejlécet, majd utána írjuk a sorbarendezett adatokat
            eredmeny.Add("filename;ID_filename;ID_PDF;egyezik-e");
            eredmeny.AddRange(sor.ToArray().OrderBy(s => s));

            string eredmenyfile = Path.Combine(dir, $"eredmeny_{DateTime.Now:yyyyMMdd_HHmmss}.CSV");
            Console.WriteLine($"Eredmény kiírása {eredmenyfile} fájlba...");
            File.WriteAllLines(eredmenyfile, eredmeny);
            Console.WriteLine("Kész.");

  
            //end
            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }


        }



        public static string ProcessFile(string filename, RectangleF? rect, string filenamepatter, string pattern, string pattern2, List<int> pages = null)
        {
            var texts = iText_GetPdfText.GetPdfTextFromPages(filename, rect, pages);
            string text = string.Join(Environment.NewLine, texts);

            string fnameonly = Path.GetFileName(filename);
            string id_in_fname = "NINCS TALÁLAT";
            string id_in_pdf = "NINCS TALÁLAT";

            //find in filename
            var matchfn = Regex.Match(fnameonly, filenamepatter, RegexOptions.IgnoreCase);
            if (matchfn.Success)
            {
                id_in_fname = matchfn.Groups[1].Value;
            }

            //find in PDF text
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                id_in_pdf = match.Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(pattern2))
                {
                    var matches = Regex.Matches(id_in_pdf, pattern2, RegexOptions.IgnoreCase);
                    if (matches.Count > 0)
                    {
                        id_in_pdf = string.Join("", matches.Cast<Match>().Select(m => m.Value)); // összes találatot egybe fűzni https://stackoverflow.com/questions/21510155/concatenates-regex-matches-to-a-string
                    }
                    else
                    { //ha nincs találat a második szűrőben, akkor ne az első eredményét adjuk vissza (vagy mégis?)
                        id_in_pdf = $"NINCS TALÁLAT (rész találat: {id_in_pdf})";
                    }
                }
            }

            return $"{fnameonly};{id_in_fname};{id_in_pdf};{id_in_fname.Equals(id_in_pdf, StringComparison.OrdinalIgnoreCase)}";
        }
    }
}
