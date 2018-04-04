using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Dropbox.Api;
using Gecko;
using Microsoft.VisualBasic;
using System.IO;
using Dropbox.Api.Files;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace QuickSave
{
    public partial class Form1 : Form
    {
        private const string appKey = "vvt67heqq5eo0ga";
        private const string authUri = @"https://www.dropbox.com/oauth2/authorize?response_type=token&redirect_uri=https://www.dropbox.com/1/oauth2/redirect_receiver&client_id=" + appKey;
        private string accessToken = "";
        private static DropboxClient dbxClient;
        private static Font boldFont = new Font(SystemFonts.DefaultFont.FontFamily, SystemFonts.DefaultFont.Size, FontStyle.Bold);

        public Form1()
        {
            InitializeComponent();
            Xpcom.Initialize("Firefox");
        }

        private bool localTokenExists()
        {
            if (Registry.CurrentUser.OpenSubKey(@"Software\QuickSaveSavva") != null)
            {
                accessToken = Registry.GetValue(@"HKEY_CURRENT_USER\Software\QuickSaveSavva", "accessToken", "").ToString();
            }
            else
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\QuickSaveSavva", "accessToken", "");
            }
            return accessToken != "";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            label1.Visible = false;
            listView1.Visible = false;
            listView1.Enabled = false;
            button1.Enabled = false;
            button1.Visible = false;
            geckoWebBrowser1.Visible = false;
            if (!localTokenExists())
            {
                MessageBox.Show("No saved user.\nYou will need to login to Dropbox first.", "First time authorization.");
                authUser();
            }
            else
            {
                disableBrowser();
                initClient();
                getCurrentFiles();
                initListView();
            }
        }

        ImageList imageList;
        private void initListView()
        {
            listView1.MultiSelect = false;
            listView1.Visible = true;
            listView1.Enabled = true;
            listView1.View = View.Details;
            listView1.GridLines = true;
            listView1.FullRowSelect = true;
            listView1.Columns.Add("", 25);
            listView1.Columns.Add("Name", 100);
            listView1.Columns.Add("Description", 250);
            listView1.Columns.Add("On PC", 180); //yes ==, yes sizeMM, no
            listView1.Columns.Add("On Cloud", 65); // ==, yes sizeMM
            listView1.View = View.Details;
            listView1.SmallImageList = imageList;
            imageList.ImageSize = new Size(16, 16);
        }

        List<string> folderName = new List<string>();
        private async void getCurrentFiles()
        {
            button1.Visible = true;
            button1.Text = "Fetching...";
            listView1.Clear();
            imageList = new ImageList();
            var list = await dbxClient.Files.ListFolderAsync(string.Empty);

            foreach (var item in list.Entries.Where(itter => itter.IsFolder))
            {
                folderName.Add(item.Name);
            }

            for (int i = 0; i < folderName.Count; i++)
            {
                list = await dbxClient.Files.ListFolderAsync("/" + folderName[i]);
                ListViewItem listItem = new ListViewItem(new string[6]);
                listItem.UseItemStyleForSubItems = false;
                if(list.Entries.Where(f => f.IsFile).Count() == 0)
                {
                    deleteFolder(folderName[i]);
                    continue;
                }
                foreach (var item in list.Entries.Where(itter => itter.IsFile))
                {
                    if (item.Name.ToString()[0] == '0')
                    {
                        downloadThumbNail("/" + folderName[i], item.Name.ToString(), listItem);
                        //image
                    }
                    else if (item.Name.ToString()[0] == '1')
                    {
                        listItem.SubItems[1].Text = item.Name.ToString().Substring(2);
                        checkMetaData("/" + folderName[i], item.Name.ToString(), listItem);
                        //file
                    }
                    else if (item.Name.ToString()[0] == '2')
                    {
                        downloadItemText("/" + folderName[i], item.Name.ToString(), listItem,5);
                        //loc
                    }
                    else if (item.Name.ToString()[0] == '3')
                    {
                        downloadItemText("/" + folderName[i], item.Name.ToString(), listItem,2);
                        //desc
                    }
                }
                listView1.Items.Add(listItem);
            }
            label1.Visible = true;
            button1.Text = "QuickSave File";
            button1.Enabled = true;
            
        }

        async void downloadThumbNail(string folder, string file, ListViewItem listItem)
        {
            using (var response = await dbxClient.Files.GetThumbnailAsync(folder + "/" + file))
            {
                imageList.Images.Add(Bitmap.FromStream(await response.GetContentAsStreamAsync()));
                listItem.ImageIndex = imageList.Images.Count - 1;
                listView1.Invalidate();
            }
        }


        async void downloadItem(string folder, string file, string loc, ListViewItem listItem)
        {
            try
            {
                button1.Enabled = false;
                button1.Text = "Downloading...";
                listView1.Enabled = false;
                Directory.CreateDirectory(loc.Substring(0, loc.LastIndexOf("\\")));
                using (var response = await dbxClient.Files.DownloadAsync(folder + "/" + file))
                {
                    File.WriteAllBytes(loc, await response.GetContentAsByteArrayAsync());
                }
                button1.Enabled = true;
                button1.Text = "QuickSave";
                listView1.Enabled = true;
                refreshListItem(listItem, 250);
                MessageBox.Show("Download Succesful", "Success");
            }
            catch(Exception e)
            {
                MessageBox.Show("Error: " + e.ToString(), "Error");
            }
        }

        async void downloadItemText(string folder, string file, ListViewItem listItem, int index)
        {
            using (var response = await dbxClient.Files.DownloadAsync(folder + "/" + file))
            {
                listItem.SubItems[index].Text = await response.GetContentAsStringAsync();
            }
        }

        private void initClient()
        {
            dbxClient = new DropboxClient(accessToken);
        }

        private void navListen(object sender, GeckoNavigatedEventArgs e)
        {
            if (e.Uri.AbsoluteUri.StartsWith(@"https://www.dropbox.com/1/oauth2/redirect_receiver") && e.Uri.ToString().Contains("access_token="))
            {
                accessToken = e.Uri.ToString().Substring(e.Uri.ToString().IndexOf("access_token=")+13, e.Uri.ToString().IndexOf("&token_type=") - e.Uri.ToString().IndexOf("access_token=")-13);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\QuickSaveSavva", "accessToken", accessToken);
                MessageBox.Show("You have successfully authorized this app on this PC.\nApplication will now restart.", "Authorized!");
                disableBrowser();
                Process.Start(Application.ExecutablePath); // to start new instance of application
                this.Close();
            }
        }

        private void disableBrowser()
        {
            geckoWebBrowser1.Visible = false;
            geckoWebBrowser1.Enabled = false;
        }

        private void authUser()
        {
            geckoWebBrowser1.Visible = true;
            geckoWebBrowser1.Navigated += navListen;
            geckoWebBrowser1.Navigate(authUri);
        }

        async void refreshListItem(ListViewItem listItem, int msDelay)
        {
            await Task.Delay(msDelay);
            checkMetaData("/" + listItem.SubItems[1].Text, "1_"+listItem.SubItems[1].Text, listItem);
        } 

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK) // Test result.
            {
                string input = "";
                string desc = "";
                string src = "";
                while (input == "")
                {
                    input = Interaction.InputBox("Give this file a name: ", "File Name", openFileDialog1.FileName.Substring(openFileDialog1.FileName.LastIndexOf("\\")+1), -1, -1);
                    if (folderName.Contains(input))
                    {
                        if(MessageBox.Show("You are about to overwrite: "+input+"\nAre you sure you want to continue", "Overwrite "+input, MessageBoxButtons.YesNo) == DialogResult.No)
                        {
                            input = "";
                        }
                    }
                }
                src = openFileDialog1.FileName;
                desc = Interaction.InputBox("Give this file a description: ", "File Description", "A brief description.", -1, -1);
                if (MessageBox.Show("Give this file an Image?\nAccepted Types: exe, ico, jpg, jpeg, bmp, or png.", "Image for " + input, MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    bool validImage = false;
                    while (!validImage)
                    {
                        if (openFileDialog1.ShowDialog() == DialogResult.OK)
                        {
                            string ext4 = openFileDialog1.FileName.Substring(openFileDialog1.FileName.Length - 4).ToLower();
                            string ext3 = ext4.Substring(1);
                            if (ext3 == "exe")
                            {
                                pictureBox1.Image = Bitmap.FromHicon(new Icon(Icon.ExtractAssociatedIcon(openFileDialog1.FileName), new Size(16, 16)).Handle);
                                validImage = true;
                            }
                            else if (ext3 == "ico")
                            {
                                pictureBox1.Image = Bitmap.FromHicon(new Icon((openFileDialog1.FileName), new Size(16, 16)).Handle);
                                validImage = true;
                            }
                            else if (ext3 == "jpg" || ext4 == "jpeg" || ext3 == "bmp" || ext3 == "png")
                            {
                                pictureBox1.Image = Image.FromFile(openFileDialog1.FileName);
                                validImage = true;
                            }
                            else
                            {
                                MessageBox.Show("Please select an exe, ico, jpg, jpeg, bmp, or png file.","Error");
                                pictureBox1.Image = null;
                                validImage = false;
                            }
                        }
                        else
                        {
                            pictureBox1.Image = null;
                            validImage = true;
                        }
                    }
                }
                upload("/" + input, input, src, desc);
            }
        }

        async void checkMetaData(string folder, string fileName, ListViewItem listItem)
        {
            while (listItem.SubItems[5].Text == "")
            {
                await Task.Delay(150);
            }
            listItem.ToolTipText = "File location: " + listItem.SubItems[5].Text;
            var metaData = await dbxClient.Files.GetMetadataAsync(folder+"/"+fileName);
            if (File.Exists(listItem.SubItems[5].Text))
            {
                bool sizeMatch = (ulong)(new FileInfo(listItem.SubItems[5].Text).Length) == metaData.AsFile.Size;
                if(sizeMatch)
                {
                    listItem.SubItems[3].Font = boldFont;
                    listItem.SubItems[3].ForeColor = Color.DarkGreen;
                    listItem.SubItems[3].Text = "True";
                    listItem.SubItems[4].Font = boldFont;
                    listItem.SubItems[4].ForeColor = Color.DarkGreen;
                    listItem.SubItems[4].Text = "True";
                }
                else
                {
                    bool localFileBigger = (ulong)(new FileInfo(listItem.SubItems[5].Text).Length) > metaData.AsFile.Size;
                    ulong byteDiff = localFileBigger ? (ulong)(new FileInfo(listItem.SubItems[5].Text).Length) - metaData.AsFile.Size : metaData.AsFile.Size - (ulong)(new FileInfo(listItem.SubItems[5].Text).Length) ;
                    if (byteDiff > 100)
                    {
                        byteDiff = 100;
                    }
                    listItem.SubItems[3].Font = boldFont;
                    listItem.SubItems[3].ForeColor = Color.DarkOrange;
                    listItem.SubItems[3].Text = localFileBigger? "True (larger by "+(byteDiff==100 ? ">= 100 bytes":byteDiff+" bytes")+")": "True (smaller by " + (byteDiff == 100 ? ">= 100 bytes" : byteDiff + " bytes") + ")";
                    listItem.SubItems[4].Font = boldFont;
                    listItem.SubItems[4].ForeColor = Color.DarkGreen;
                    listItem.SubItems[4].Text = "True";
                }
            }
            else
            {
                listItem.SubItems[3].Font = boldFont;
                listItem.SubItems[3].ForeColor = Color.DarkRed;
                listItem.SubItems[3].Text = "False";
                listItem.SubItems[4].Font = boldFont;
                listItem.SubItems[4].ForeColor = Color.DarkGreen;
                listItem.SubItems[4].Text = "True";
            }
        }

        private void listView1_MouseDoubleClick(Object sender, MouseEventArgs e)
        {
            if (listView1.Enabled && listView1.SelectedItems.Count == 1)
            {
                ListViewItem selected = listView1.SelectedItems[0];
                if (selected.SubItems[3].Text == "False")
                {
                    MessageBox.Show("Downloading " + selected.SubItems[1].Text + " to " + selected.SubItems[5].Text);
                    downloadItem("/"+selected.SubItems[1].Text, "1_" + selected.SubItems[1].Text, selected.SubItems[5].Text, selected);
                }
                else if(selected.SubItems[3].Text != "True")
                {
                    if (MessageBox.Show("Replace local file with cloud file?\nYour local file will be saved as a .bak.\nAny older .bak files will be overwritten.", "Replace", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        if (File.Exists(selected.SubItems[5].Text))
                        {
                            File.Copy(selected.SubItems[5].Text, selected.SubItems[5].Text + ".bak", true);
                        }
                        downloadItem("/" + selected.SubItems[1].Text, "1_" + selected.SubItems[1].Text, selected.SubItems[5].Text, selected);
                    }
                    else if (MessageBox.Show("Replace cloud file with local file?", "Replace", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        uploadUpdate("/" + selected.SubItems[1].Text, selected.SubItems[1].Text, selected.SubItems[5].Text, selected);
                    }
                }
                else
                {
                    refreshListItem(selected, 30);
                }
            }
        }

        ListViewItem rightClickedListItem = null;
        private void listView1_MouseRightClick(object sender, MouseEventArgs e)
        {
            if (listView1.Enabled && listView1.SelectedItems.Count == 1 && e.Button == MouseButtons.Right)
            {
                rightClickedListItem = listView1.SelectedItems[0];
                if (rightClickedListItem == null || rightClickedListItem.SubItems[1].Text == "" ||  rightClickedListItem.SubItems[3].Text == "" || rightClickedListItem.SubItems[4].Text == "" || rightClickedListItem.SubItems[5].Text == "")
                {
                    for(int i = 0; i < contextMenuStrip1.Items.Count; i++)
                    {
                        contextMenuStrip1.Items[i].Enabled = false;
                    }
                }
                else
                {
                    for (int i = 0; i < contextMenuStrip1.Items.Count; i++)
                    {
                        contextMenuStrip1.Items[i].Enabled = true;
                    }
                    if (!File.Exists(rightClickedListItem.SubItems[5].Text))
                    {
                        contextMenuStrip1.Items[3].Enabled = false;
                        contextMenuStrip1.Items[5].Enabled = false;
                    }
                }
                contextMenuStrip1.Show(Cursor.Position);
            }
        }

        async void uploadUpdate(string folder, string fileName, string sourceFilePath, ListViewItem listItem)
        {
            button1.Enabled = false;
            button1.Text = "Uploading...";
            listView1.Enabled = false;
            using (var mem = new MemoryStream(File.ReadAllBytes(sourceFilePath)))
            {
                var updated = await dbxClient.Files.UploadAsync(folder + "/1_" + fileName, WriteMode.Overwrite.Instance, body: mem);
            }
            listView1.Enabled = true;
            button1.Text = "QuickSave";
            button1.Enabled = true;
            refreshListItem(listItem, 250);
            MessageBox.Show("Upload Success!", "Success");
        }

        async void upload(string folder, string fileName, string sourceFilePath, string fileDesc)
        {
            button1.Enabled = false;
            listView1.Enabled = false;
            bool b1 = false;
            bool b2 = false;
            bool b3 = false;
            bool b4 = false;
            await dbxClient.Files.CreateFolderV2Async(folder);
            using (var mem = new MemoryStream(File.ReadAllBytes(sourceFilePath)))
            {
                button1.Text = "Uploading File...";
                var updated = await dbxClient.Files.UploadAsync(folder + "/1_" + fileName, WriteMode.Overwrite.Instance,body: mem);
                b1 = true;
            }
            using (var mem = new MemoryStream(Encoding.UTF8.GetBytes(sourceFilePath)))
            {
                button1.Text = "Uploading Dir...";
                var updated = await dbxClient.Files.UploadAsync(folder + "/2_loc.txt", WriteMode.Overwrite.Instance, body: mem);
                b2 = true;
            }
            if (pictureBox1.Image != null)
            {
                var destImage = new Bitmap(16, 16);

                destImage.SetResolution(pictureBox1.Image.HorizontalResolution, pictureBox1.Image.VerticalResolution);

                using (var graphics = Graphics.FromImage(destImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using (var wrapMode = new ImageAttributes())
                    {
                        wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                        graphics.DrawImage(pictureBox1.Image, new Rectangle(0, 0, 16, 16), 0, 0, pictureBox1.Image.Width, pictureBox1.Image.Height, GraphicsUnit.Pixel, wrapMode);
                    }
                }
                MemoryStream ms = new MemoryStream();
                destImage.Save(ms, ImageFormat.Jpeg);
                byte[] buff = ms.GetBuffer();
                using (var mem = new MemoryStream(buff))
                {
                    button1.Text = "Uploading Image...";
                    var updated = await dbxClient.Files.UploadAsync(folder + "/0_img.jpeg", WriteMode.Overwrite.Instance, body: mem);
                    b3 = true;
                }
            }
            if (fileDesc.Trim() != "")
            {
                using (var mem = new MemoryStream(Encoding.UTF8.GetBytes(fileDesc)))
                {
                    button1.Text = "Uploading Desc...";
                    var updated = await dbxClient.Files.UploadAsync(folder + "/3_desc.txt", WriteMode.Overwrite.Instance, body: mem);
                    b4 = true;
                }
            }
            if ((b1 && b2 && (b3 || (!b3 && pictureBox1.Image == null)) && (b4 || (!b4 && fileDesc.Trim() == ""))))
            {
                ListViewItem listItem = new ListViewItem(new string[6]);
                listItem.UseItemStyleForSubItems = false;
                listItem.SubItems[1].Text = fileName;
                listItem.SubItems[2].Text = fileDesc;
                listItem.SubItems[3].Text = "True";
                listItem.SubItems[3].Font = boldFont;
                listItem.SubItems[3].ForeColor = Color.DarkGreen;
                listItem.SubItems[4].Text = "True";
                listItem.SubItems[4].Font = boldFont;
                listItem.SubItems[4].ForeColor = Color.DarkGreen;
                listItem.SubItems[5].Text = sourceFilePath;
                if (b3)
                {
                    imageList.Images.Add(new Bitmap(pictureBox1.Image));
                    listItem.ImageIndex = imageList.Images.Count - 1;
                }
                else
                {
                    pictureBox1.Image = null;
                }
                listItem.ToolTipText = "File location: " + listItem.SubItems[5].Text;
                listView1.Items.Add(listItem);
                listView1.Enabled = true;
                button1.Text = "QuickSave";
                button1.Enabled = true;
                MessageBox.Show("Upload completed.", "Success");
            }
            else
            {
                string s = (b1 ? "" : "File,") + (b2 ? "" : " File Location,") + (b3 ? "" : " File Description,") + ((b3||(!b3 && pictureBox1.Image==null)) ? "" : " File Image");
                if (s[s.Length - 1] == ',')
                {
                    s.Substring(0, s.Length - 1);
                }
                await dbxClient.Files.DeleteV2Async(folder);
                listView1.Enabled = true;
                button1.Text = "QuickSave";
                button1.Enabled = true;
                MessageBox.Show("Couldn't save: "+s, "Failed");
            }
        }

        private void forceUploadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            uploadUpdate("/" + rightClickedListItem.SubItems[1].Text, rightClickedListItem.SubItems[1].Text, rightClickedListItem.SubItems[5].Text, rightClickedListItem);
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            refreshListItem(rightClickedListItem, 30);
        }

        //forceDownload
        private void forToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (File.Exists(rightClickedListItem.SubItems[5].Text))
            {
                File.Copy(rightClickedListItem.SubItems[5].Text, rightClickedListItem.SubItems[5].Text + ".bak", true);
            }
            downloadItem("/" + rightClickedListItem.SubItems[1].Text, "1_" + rightClickedListItem.SubItems[1].Text, rightClickedListItem.SubItems[5].Text, rightClickedListItem);
        }

        private void openFileLocationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string filePath = rightClickedListItem.SubItems[5].Text.Substring(0, rightClickedListItem.SubItems[5].Text.LastIndexOf("\\")+1);
            if (Directory.Exists(filePath))
            {
                Process.Start("explorer.exe", filePath);
            }
            else
            {
                MessageBox.Show(filePath + " can't be found.","Error No Path");
            }
        }

        private void deleteFromDiskToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (File.Exists(rightClickedListItem.SubItems[5].Text))
            {
                File.Delete(rightClickedListItem.SubItems[5].Text);
                refreshListItem(rightClickedListItem, 30);
            }
        }

        async void deleteFolder(string folder)
        {
            await dbxClient.Files.DeleteV2Async("/"+folder);
            listView1.Items.Remove(rightClickedListItem);
            rightClickedListItem = null;
        }

        private void deleteFromCloudToolStripMenuItem_Click(object sender, EventArgs e)
        {
            deleteFolder(rightClickedListItem.SubItems[1].Text);
        }
    }
}
