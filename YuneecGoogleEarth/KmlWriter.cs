using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace KmlWriter
{
    public class KmlWriter
    {
        private readonly string _pointTemplate;
        private readonly string _pathTemplate;

        private readonly string _pointFileName = @"KML\Quad_Position.kml";
        private readonly string _pathFilename = @"KML\Quad_Path.kml";


        public bool KmlEnabled => string.IsNullOrEmpty(_pointTemplate) == false;

        public KmlWriter()
        {
            _pointTemplate = ReadKmlTemplate(@"KML\Template_point.kml");
            _pathTemplate = ReadKmlTemplate(@"KML\Template_path.kml");
        }


        private string ReadKmlTemplate(string path)
        {
            if (File.Exists(path))
            {
                using (var sr = new StreamReader(path))
                {
                    return sr.ReadToEnd();
                }
            }
            return "";
        }


        public void UpdateKmlPath(List<Tuple<double, double, double>> coordinates, string name)
        {
            if (string.IsNullOrEmpty(_pathTemplate)) return;
            try
            {
                var sb = new StringBuilder();

                if (coordinates != null)
                {
                    foreach (var line in coordinates)
                    {
                        var lat = line.Item1;
                        var lon = line.Item2;
                        var alt = line.Item3;

                        sb.AppendLine($"{lon},{lat},{alt}");
                    }
                }

                using (var sw = new StreamWriter(_pathFilename))
                {
                    var temp = _pathTemplate.Replace("%COORDS", sb.ToString());
                    temp = temp.Replace("%NAME", name);

                    sw.Write(temp);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }


        public void UpdateKmlVehicle(double lat, double lon, double alt, double heading, string info, string name)
        {
            if (string.IsNullOrEmpty(_pointTemplate)) return;

            try
            {
                using (var sw = new StreamWriter(_pointFileName))
                {
                    var temp = _pointTemplate.Replace("%LON", lon.ToString(CultureInfo.InvariantCulture));
                    temp = temp.Replace("%LAT", lat.ToString(CultureInfo.InvariantCulture));
                    temp = temp.Replace("%ALT", alt.ToString(CultureInfo.InvariantCulture));
                    temp = temp.Replace("%HDG", heading.ToString(CultureInfo.InvariantCulture));
                    temp = temp.Replace("%DATA", info);
                    temp = temp.Replace("%NAME", name);

                    sw.Write(temp);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

    }
}