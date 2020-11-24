using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace PDF_Content_vs_Filename_Checker
{
    [Serializable]
    public class Options
    {
        [XmlIgnore]
        public List<int> PageList { get; private set; } = new List<int> { -1 };

        public string Pages
        {
            get => string.Join(",", PageList);
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    PageList = new List<int> { 0 }; //if it was empty, then we set 0, indicating that all pages should be processed
                }
                else
                {
                    PageList = new List<int>();
                    string[] parts = (value ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries); // slpit by separators (comma, semicolon)
                    foreach (string part in parts)
                    {
                        if (part.Contains('-'))
                        {
                            var bounds = part.Split(new char[] { '-' }, 2).Select(int.Parse).ToList();
                            if (bounds[0] > bounds[1])
                            {
                                throw new InvalidOperationException($"Invalid Pages range format! ({part} is not a valid range)");
                            }
                            for (int i = bounds[0]; i <= bounds[1]; i++)
                            {
                                PageList.Add(i);
                            }
                        }
                        else
                        {
                            PageList.Add(int.Parse(part));
                        }

                    }
                    //PageList = (value ?? "").Split(',').Select(int.Parse).ToList();  //https://stackoverflow.com/questions/1763613/convert-comma-separated-string-of-ints-to-int-array
                }

            }
        }

        public string FileNameSearchPattern { get; set; }

        public string PdfContentSearchPattern { get; set; }

        public string PdfContentSearchPattern2 { get; set; } //search in the first result (sometimes the format is differnt, we have to filter out some characters from the result)

        [TypeConverter(typeof(RectangleFConverter))]
        public RectangleF? SearchRegion { get; set; } = null;




        //parameterless ctor needed for XML deserializer
        public Options() { }


        public void SaveToXml(string path)
        {
            //skip redundancy from RectangleF
            XmlAttributeOverrides overrides = new XmlAttributeOverrides();
            XmlAttributes attribs = new XmlAttributes();
            attribs.XmlIgnore = true;
            attribs.XmlElements.Add(new XmlElementAttribute("Location"));
            overrides.Add(typeof(RectangleF), "Location", attribs);
            attribs.XmlElements.Add(new XmlElementAttribute("Size"));
            overrides.Add(typeof(RectangleF), "Size", attribs);
            //end skip redundancy from RectangleF


            XmlSerializer xmls = new XmlSerializer(this.GetType(), overrides, typeof(Options).GetNestedTypes(), null, "");
            using (FileStream fs = File.Create(path))
            {
                xmls.Serialize(fs, this);
            }
        }

        public static Options CreateFromXml(string path)
        {
            //itt a 2. paraméter megoldja azt, hogy ne dobjon egy kivételt (ami bár csak a konzol logban jelenik meg, ha debuggerből fut, de engem mégis zavar)
            // https://stackoverflow.com/a/39513223/7200765
            XmlSerializer xmls = new XmlSerializer(typeof(Options), typeof(Options).GetNestedTypes());
            using (StreamReader reader = new StreamReader(path))
            {
                //values should be validated by the caller!
                return (Options)xmls.Deserialize(reader);
            }
        }

        public static Options ExampleDefault()
        {
            return new Options
            {
                PageList = new List<int> { 1 },
                FileNameSearchPattern = @"_(\d{8})_",
                PdfContentSearchPattern = @"azonosító:\s*?(\d{8})",
                SearchRegion = null,
            };
        }

    }



    #region TypeConverters for simplifying XML serailization
    //TODO: cite stackoverflow source for this
    public class PointFConverter : TypeConverter
    {

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return ((sourceType == typeof(string)) || base.CanConvertFrom(context, sourceType));
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return ((destinationType == typeof(InstanceDescriptor)) || base.CanConvertTo(context, destinationType));
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string str = value as string;
            if (value == null) return base.ConvertFrom(context, culture, value);
            str = str.Trim();
            if (str.Length == 0) return null;
            if (culture == null) culture = CultureInfo.CurrentCulture;
            char ch = culture.TextInfo.ListSeparator[0];
            string[] strArray = str.Split(new char[] { ch });
            int[] numArray = new int[strArray.Length];
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(float));
            if (numArray.Length != 2) throw new ArgumentException("Invalid format");
            for (int i = 0; i < numArray.Length; i++)
            {
                numArray[i] = (int)converter.ConvertFromString(context, culture, strArray[i]);
            }
            return new PointF(numArray[0], numArray[1]);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == null) throw new ArgumentNullException("destinationType");
            if (value is Point)
            {
                if (destinationType == typeof(string))
                {
                    PointF point = (PointF)value;
                    if (culture == null) culture = CultureInfo.CurrentCulture;
                    string separator = culture.TextInfo.ListSeparator + " ";
                    TypeConverter converter = TypeDescriptor.GetConverter(typeof(float));
                    string[] strArray = new string[2];
                    int num = 0;
                    strArray[num++] = converter.ConvertToString(context, culture, point.X);
                    strArray[num++] = converter.ConvertToString(context, culture, point.Y);
                    return string.Join(separator, strArray);
                }
                if (destinationType == typeof(InstanceDescriptor))
                {
                    PointF point2 = (PointF)value;
                    ConstructorInfo constructor = typeof(PointF).GetConstructor(new Type[] { typeof(float), typeof(float) });
                    if (constructor != null) return new InstanceDescriptor(constructor, new object[] { point2.X, point2.Y });
                }
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object CreateInstance(ITypeDescriptorContext context, IDictionary propertyValues)
        {
            if (propertyValues == null) throw new ArgumentNullException("propertyValues");
            object xvalue = propertyValues["X"];
            object yvalue = propertyValues["Y"];
            if (((xvalue == null) || (yvalue == null)) || (!(xvalue is float) || !(yvalue is float)))
            {
                throw new ArgumentException("Invalid property value entry");
            }
            return new PointF((float)xvalue, (float)yvalue);
        }

        public override bool GetCreateInstanceSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            return TypeDescriptor.GetProperties(typeof(PointF), attributes).Sort(new string[] { "X", "Y" });
        }

        public override bool GetPropertiesSupported(ITypeDescriptorContext context)
        {
            return true;
        }
    }


    public class RectangleFConverter : TypeConverter
    {

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return ((sourceType == typeof(string)) || base.CanConvertFrom(context, sourceType));
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return ((destinationType == typeof(InstanceDescriptor)) || base.CanConvertTo(context, destinationType));
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string str = value as string;
            if (value == null) return base.ConvertFrom(context, culture, value);
            str = str.Trim();
            if (str.Length == 0) return null;
            if (culture == null) culture = CultureInfo.CurrentCulture;
            char ch = culture.TextInfo.ListSeparator[0];
            string[] strArray = str.Split(new char[] { ch });
            int[] numArray = new int[strArray.Length];
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(float));
            for (int i = 0; i < numArray.Length; i++)
            {
                numArray[i] = (int)converter.ConvertFromString(context, culture, strArray[i]);
            }
            if (numArray.Length != 4) throw new ArgumentException("Invalid format");
            return new RectangleF(numArray[0], numArray[1], numArray[2], numArray[3]);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == null) throw new ArgumentNullException("destinationType");
            if (value is Rectangle)
            {
                if (destinationType == typeof(string))
                {
                    RectangleF rect = (RectangleF)value;
                    if (culture == null) culture = CultureInfo.CurrentCulture;
                    string separator = culture.TextInfo.ListSeparator + " ";
                    TypeConverter converter = TypeDescriptor.GetConverter(typeof(float));
                    string[] strArray = new string[4];
                    int num = 0;
                    strArray[num++] = converter.ConvertToString(context, culture, rect.X);
                    strArray[num++] = converter.ConvertToString(context, culture, rect.Y);
                    strArray[num++] = converter.ConvertToString(context, culture, rect.Width);
                    strArray[num++] = converter.ConvertToString(context, culture, rect.Height);
                    return string.Join(separator, strArray);
                }
                if (destinationType == typeof(InstanceDescriptor))
                {
                    RectangleF rect2 = (RectangleF)value;
                    ConstructorInfo constructor = typeof(RectangleF).GetConstructor(new Type[] { typeof(float), typeof(float), typeof(float), typeof(float) });
                    if (constructor != null) return new InstanceDescriptor(constructor, new object[] { rect2.X, rect2.Y, rect2.Width, rect2.Height });
                }
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        public override object CreateInstance(ITypeDescriptorContext context, IDictionary propertyValues)
        {
            if (propertyValues == null) throw new ArgumentNullException("propertyValues");
            object xvalue = propertyValues["X"];
            object yvalue = propertyValues["Y"];
            object wvalue = propertyValues["Width"];
            object hvalue = propertyValues["Height"];
            if (((xvalue == null) || (yvalue == null)) || (!(xvalue is float) || !(yvalue is float)))
            {
                throw new ArgumentException("Invalid property value entry");
            }
            return new RectangleF((float)xvalue, (float)yvalue, (float)wvalue, (float)hvalue);
        }

        public override bool GetCreateInstanceSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            return TypeDescriptor.GetProperties(typeof(RectangleF), attributes).Sort(new string[] { "X", "Y", "Width", "Height" });
        }

        public override bool GetPropertiesSupported(ITypeDescriptorContext context)
        {
            return true;
        }
    }

    #endregion
}
