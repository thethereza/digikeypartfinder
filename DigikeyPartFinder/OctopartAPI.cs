/*
 * ReThink Digikey Lookup
 Copyright (C) 2014  Reza Naima <reza@rethinkmedical.com>

 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigikeyPartFinder.Properties;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Specialized;
using System.Web;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Text.RegularExpressions;


namespace DigikeyPartFinder {

    public class Result {
        public string name;
        public Dictionary<int, float> prices = new Dictionary<int, float>();
        public string sku;
        public string mpn;
        public int inventory;
        public float value;
        public float voltageRating;
        public string tolerance;
        public string package;
        public string dielectric = "";

        public Result() {
            prices[1] = 0f;
            prices[10] = 0f;
            prices[25] = 0f;
            prices[50] = 0f;
            prices[100] = 0f;
        }

        public void Finish() {
            if (prices[10] == 0) prices[10] = prices[1];
            if (prices[25] == 0) prices[25] = prices[10];
            if (prices[50] == 0) prices[50] = prices[25];
            if (prices[100] == 0) prices[100] = prices[50];
        }
    }


    public class OctopartAPI {
        private string url = "http://octopart.com/api/v3/parts/search";
        public enum ComponentType {
            Capacitor,
            Resistor
        };

        private string ToScientific(string str) {
            Regex r = new Regex(@"([\.0-9]+)([mkunpMG])");
            Match m = r.Match(str);
            if (m.Success) {
                double val = double.Parse(m.Groups[1].Value);
                switch (m.Groups[2].Value.ToString()) {
                    case "m": val *= .001; break;
                    case "k": val *= 1000; break;
                    case "u": val *= .000001; break;
                    case "n": val *= .000000001; break;
                    case "p": val *= .000000000001; break;
                    case "M": val *= 1000000; break;
                    case "G": val *= 1000000000; break;
                }
                return val.ToString("E3");
            }
            r = new Regex(@"([\.0-9]+)$");
            m = r.Match(str);
            if (m.Success) {
                float val = float.Parse(m.Groups[1].Value.ToString());
                return val.ToString("E3");
            }
            throw new Exception("Unable to parse " + str);
        }


        private string ToQueryString(NameValueCollection nvc) {
            var array = (from key in nvc.AllKeys
                         from value in nvc.GetValues(key)
                         select string.Format("{0}={1}",
                         HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value))).ToArray();
            return "?" + string.Join("&", array);
        }


        public List<Result> Get(string size, string value, ComponentType type) {
            NameValueCollection nvc = new NameValueCollection();
            if (Settings.Default.API_KEY == null) {
                MessageBox.Show("Set API Key First (under file)");
                return null;
            }
            nvc.Add("apikey", Settings.Default.API_KEY);
            nvc.Add("start", "0");
            nvc.Add("limit", "50");
            nvc.Add("sortby", "avg_price asc");
            nvc.Add("filter[queries][]", "offers.seller.name:Digi-Key");
            nvc.Add("include[]", "specs");
            nvc.Add("filter[queries][]", "specs.case_package.value:" + size);

            switch (type) {
                case ComponentType.Capacitor:
                    nvc.Add("q", "CAPACITOR");
                    nvc.Add("filter[queries][]", "specs.dielectric_characteristic.value:(X5R or X7R or C0G/NP0)");
                    nvc.Add("filter[queries][]", "specs.capacitance.value:" + ToScientific(value));
                    break;
                case ComponentType.Resistor:
                    nvc.Add("q", "RESISTOR 1%");
                    nvc.Add("filter[queries][]", "specs.resistance.value:" + ToScientific(value));
                   // nvc.Add("filter[queries][]", "specs.resistance_tolerance.value:±1%");
                    break;
            }


            string q = url + ToQueryString(nvc);
            string response;
            using (var wb = new WebClient()) {
                response = wb.DownloadString(q);
            }
            Clipboard.SetText(response);
            List<Result> r = new List<Result>();

            Dictionary<string, object> dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
            Newtonsoft.Json.Linq.JArray results = (Newtonsoft.Json.Linq.JArray)dict["results"];
            foreach (var result in results) {
                var item = result["item"];
                foreach (var offer in item["offers"]) {
                    // make sure we are interested
                    if (!offer["seller"]["name"].ToString().Equals("Digi-Key")) continue;
                    if (!offer["packaging"].ToString().Equals("Cut Tape")) continue;
                    if (int.Parse(offer["in_stock_quantity"].ToString()) < 1000) continue;

                    Result rr = new Result();
                    rr.name = HtmlRemoval.StripTagsRegex(result["snippet"].ToString());
                    rr.mpn = item["mpn"].ToString();
                    rr.sku = offer["sku"].ToString();
                    rr.inventory = (int)offer["in_stock_quantity"];
                    try {
                        rr.package = item["specs"]["case_package"]["value"][0].ToString();
                    } catch {
                        rr.package = "";
                    }

                    if (type == ComponentType.Capacitor) {
                        rr.value = float.Parse(item["specs"]["capacitance"]["value"][0].ToString());
                        rr.voltageRating = (float)(item["specs"]["voltage_rating_dc"]["value"][0]);
                        try { rr.tolerance = item["specs"]["capacitance_tolerance"]["value"][0].ToString(); } catch { rr.tolerance = ""; }
                        try { rr.dielectric = item["specs"]["dielectric_characteristic"]["value"][0].ToString(); } catch { rr.dielectric = ""; }
                    }
                    if (type == ComponentType.Resistor) {
                        rr.value = float.Parse(item["specs"]["resistance"]["value"][0].ToString());
                        try { rr.tolerance = item["specs"]["resistance_tolerance"]["value"][0].ToString(); } catch { rr.tolerance = ""; }
                    }

                    foreach (var price in offer["prices"]["USD"]) {
                        if (rr.prices.ContainsKey((int)price[0])) rr.prices[(int)price[0]] = (float)price[1];
                        else rr.prices.Add((int)price[0], (float)price[1]);
                    }

                    rr.Finish();
                    if (rr.prices.Count > 0) r.Add(rr);

                }
            }
            return r;
        }

    }

    public static class HtmlRemoval {
        public static string StripTagsRegex(string source) {
            return Regex.Replace(source, "<.*?>", string.Empty);
        }
    }
}