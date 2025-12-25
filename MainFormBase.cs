using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SkiaSharp;

namespace PngToWebp
{
    public class MainFormBase : Form
    {
        private ListBox lstFiles;
        private Button btnAddFiles, btnAddFolder, btnRemove, btnClear, btnChooseOut, btnConvert;
        private CheckBox /*chkLossless, */ chkOverwrite;
        private NumericUpDown numQuality;
        private Label lblQuality, lblOutDir, lblStatus;
        private TextBox txtOutDir;
        private ProgressBar progress;
        private FolderBrowserDialog fbd;
        private OpenFileDialog ofd;

        public MainFormBase()
        {
            Text = "PNG → WebP Converter";
            Width = 900;
            Height = 600;
            StartPosition = FormStartPosition.CenterScreen;
            AllowDrop = true;

            lstFiles = new ListBox { Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, Left = 20, Top = 20, Width = 620, Height = 460 };
            lstFiles.HorizontalScrollbar = true;

            btnAddFiles  = new Button { Left = 660, Top = 20,  Width = 200, Height = 34, Text = "파일 추가 (PNG)" };
            btnAddFolder = new Button { Left = 660, Top = 60,  Width = 200, Height = 34, Text = "폴더 추가 (PNG 스캔)" };
            btnRemove    = new Button { Left = 660, Top = 100, Width = 95,  Height = 34, Text = "선택 제거" };
            btnClear     = new Button { Left = 765, Top = 100, Width = 95,  Height = 34, Text = "전체 비우기" };

            lblOutDir = new Label { Left = 20, Top = 490, Width = 70, Text = "출력 폴더" };
            txtOutDir = new TextBox { Left = 90, Top = 486, Width = 470, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            btnChooseOut = new Button { Left = 570, Top = 484, Width = 70, Height = 30, Text = "선택" };

            lblQuality = new Label { Left = 660, Top = 150, Width = 40, Text = "품질" };
            numQuality = new NumericUpDown { Left = 700, Top = 146, Width = 80, Minimum = 0, Maximum = 100, Value = 100 };
            // chkLossless = new CheckBox { Left = 790, Top = 148, Width = 80, Text = "무손실" };
            chkOverwrite = new CheckBox { Left = 660, Top = 180, Width = 200, Text = "덮어쓰기 허용(같은 이름)" };

            btnConvert = new Button { Left = 660, Top = 220, Width = 200, Height = 40, Text = "변환 시작" };

            progress = new ProgressBar { Left = 20, Top = 525, Width = 620, Height = 22, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            lblStatus = new Label { Left = 660, Top = 525, Width = 200, Height = 22, Text = "대기" };

            Controls.AddRange(new Control[] { lstFiles, btnAddFiles, btnAddFolder, btnRemove, btnClear, lblOutDir, txtOutDir, btnChooseOut, lblQuality, numQuality, /*chkLossless, */ chkOverwrite, btnConvert, progress, lblStatus });

            fbd = new FolderBrowserDialog();
            ofd = new OpenFileDialog { Filter = "PNG 이미지|*.png", Multiselect = true, Title = "PNG 파일 선택" };

            btnAddFiles.Click += (s, e) => AddFiles();
            btnAddFolder.Click += (s, e) => AddFolder();
            btnRemove.Click += (s, e) => RemoveSelected();
            btnClear.Click += (s, e) => lstFiles.Items.Clear();
            btnChooseOut.Click += (s, e) => ChooseOutDir();
            btnConvert.Click += async (s, e) => await ConvertAllAsync();

            DragEnter += MainForm_DragEnter;
            DragDrop  += MainForm_DragDrop;

            // 기본 출력 폴더는 입력 파일과 동일(빈 경우 변환 시 자동 결정)
            txtOutDir.Text = "";
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
            {
                var pngs = new List<string>();
                foreach (var p in paths)
                {
                    if (File.Exists(p) && IsPng(p)) pngs.Add(p);
                    else if (Directory.Exists(p)) pngs.AddRange(EnumeratePngs(p));
                }
                AddToListDistinct(pngs);
            }
        }

        private void AddFiles()
        {
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                AddToListDistinct(ofd.FileNames.Where(IsPng));
            }
        }

        private void AddFolder()
        {
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                var pngs = EnumeratePngs(fbd.SelectedPath);
                AddToListDistinct(pngs);
            }
        }

        private void RemoveSelected()
        {
            var sel = lstFiles.SelectedItems.Cast<object>().ToList();
            foreach (var it in sel) lstFiles.Items.Remove(it);
        }

        private void ChooseOutDir()
        {
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                txtOutDir.Text = fbd.SelectedPath;
            }
        }

        private static bool IsPng(string path)
            => string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<string> EnumeratePngs(string folder)
            => Directory.EnumerateFiles(folder, "*.png", SearchOption.AllDirectories);

        private void AddToListDistinct(IEnumerable<string> files)
        {
            var set = new HashSet<string>(lstFiles.Items.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                if (set.Add(f)) lstFiles.Items.Add(f);
            }
        }

        private async Task ConvertAllAsync()
        {
            if (lstFiles.Items.Count == 0)
            {
                MessageBox.Show("변환할 PNG 파일을 추가해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnConvert.Enabled = false;
            lblStatus.Text = "변환 중...";
            progress.Value = 0;
            progress.Maximum = lstFiles.Items.Count;

            var outBase = txtOutDir.Text?.Trim();
            if (!string.IsNullOrEmpty(outBase) && !Directory.Exists(outBase))
            {
                try { Directory.CreateDirectory(outBase); }
                catch (Exception ex)
                {
                    MessageBox.Show($"출력 폴더 생성 실패:\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnConvert.Enabled = true;
                    lblStatus.Text = "오류";
                    return;
                }
            }

            int ok = 0, fail = 0;
            var quality = (int)numQuality.Value;
            // var lossless = chkLossless.Checked;
            var overwrite = chkOverwrite.Checked;

            foreach (var item in lstFiles.Items.Cast<string>())
            {
                try
                {
                    var src = item;
                    var outDir = string.IsNullOrEmpty(outBase) ? Path.GetDirectoryName(src)! : outBase;
                    var nameNoExt = Path.GetFileNameWithoutExtension(src);
                    var dst = Path.Combine(outDir, nameNoExt + ".webp");

                    if (!overwrite)
                    {
                        // 중복 이름 회피: name.webp, name(1).webp, ...
                        dst = NextAvailableName(dst);
                    }

                    await Task.Run(() => ConvertOnePngToWebp(src, dst, quality /*, lossless*/));
                    ok++;
                }
                catch
                {
                    fail++;
                }
                progress.Value += 1;
                lblStatus.Text = $"진행: {progress.Value}/{progress.Maximum} (성공 {ok}, 실패 {fail})";
                await Task.Yield();
            }

            lblStatus.Text = $"완료: 성공 {ok}, 실패 {fail}";
            btnConvert.Enabled = true;

            if (fail == 0)
                MessageBox.Show($"변환 완료!\n성공 {ok}개", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show($"변환 완료(일부 실패)\n성공 {ok}개, 실패 {fail}개", "완료", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string NextAvailableName(string path)
        {
            if (!File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(path)!;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            int i = 1;
            string tryPath;
            do
            {
                tryPath = Path.Combine(dir, $"{name}({i++}){ext}");
            } while (File.Exists(tryPath));
            return tryPath;
        }

        private static void ConvertOnePngToWebp(string srcPath, string dstPath, int quality /*, bool lossless*/)
        {
            // PNG 디코드
            using var input = File.OpenRead(srcPath);
            using var codec = SKCodec.Create(input);
            if (codec == null) throw new InvalidOperationException("이미지를 열 수 없습니다.");

            var info = codec.Info;
            using var bitmap = new SKBitmap(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
                throw new InvalidOperationException("픽셀 디코딩 실패");

            using var image = SKImage.FromBitmap(bitmap);
            
            // WebP 인코드 옵션
            var data = image.Encode(SKEncodedImageFormat.Webp, quality);

            if (data == null) throw new InvalidOperationException("WebP 인코딩 실패");

            Directory.CreateDirectory(Path.GetDirectoryName(dstPath)!);
            using var fs = File.Create(dstPath);
            data.SaveTo(fs);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            // 
            // MainFormBase
            // 
            ClientSize = new System.Drawing.Size(719, 308);
            ResumeLayout(false);
        }
    }
}
