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
    public partial class Form1: Form {
        OctopartAPI api;
        public bool quit = false;

        public Form1() {
            if (!Settings.Default.GPL) {
                License l = new License();
                l.ShowDialog();
                if (!Settings.Default.GPL) {
                    quit = true;
                    Application.Exit();
                    this.Close();
                }
            } 

            InitializeComponent();
            api = new OctopartAPI();
            comboBox_size.SelectedIndex = 0;
            comboBox_type.SelectedIndex = 0;
        }


        private void button1_Click(object sender, EventArgs e) {
            string type = comboBox_type.SelectedItem.ToString();
            string size = comboBox_size.SelectedItem.ToString();
            string value = textBox_value.Text;

            List<Result> res = api.Get(size, value, (comboBox_type.SelectedIndex == 0) ? OctopartAPI.ComponentType.Capacitor : OctopartAPI.ComponentType.Resistor);
            dataGridView.RowCount = 0; //clear
            foreach (var r in res) {
                dataGridView.Rows.Add(r.name, r.value + r.tolerance, r.package +" "+ r.dielectric, r.sku, r.mpn, r.inventory, r.prices[1], r.prices[10], r.prices[25], r.prices[50], r.prices[100]);
            }
            //deault to sort by column qty 10
            dataGridView.Sort(dataGridView.Columns[7], ListSortDirection.Ascending);

            //auto-resize
            for (int i = 0; i < dataGridView.Columns.Count; i++) {
                dataGridView.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
        }

        private void setupKeysToolStripMenuItem_Click(object sender, EventArgs e) {
            APIConfigure apic = new APIConfigure();
            apic.Show();
        }

        private void dataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e) {
            try {
                object value = dataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                switch (e.ColumnIndex) {
                    case 3:
                        System.Diagnostics.Process.Start("http://www.digikey.com/product-search/en?lang=en&site=us&KeyWords=" + value.ToString());
                        break;
                    case 4:
                        Clipboard.SetText(value.ToString());
                        break;
                }
            } catch (Exception ee) {
            }
        }

        private void aboutToolStripMenuItem1_Click(object sender, EventArgs e) {
            MessageBox.Show("v0.1\r\nUse At Your Own Risk","ReThink Digikey Lookup");
        }


    }

  

}
