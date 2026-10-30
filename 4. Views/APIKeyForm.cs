using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using LOSTARK_GEM_FINDER.Core;

namespace LOSTARK_GEM_FINDER.Views
{
    // 코드로만 UI를 구성하는 API 키 입력 창
    public class APIKeyForm : Form
    {
        private readonly IAPIKeyStore _keyStore;
        private TextBox txtApiKey;

        public string ApiKey { get; private set; }

        public APIKeyForm(IAPIKeyStore keyStore)
        {
            _keyStore = keyStore;

            this.Text = "API 키 입력";
            this.ClientSize = new Size(500, 150);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lblInfo = new Label
            {
                Text = "경매장 시세를 조회하려면 로스트아크 개발자 센터의 API 키가 필요해요.",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("맑은 고딕", 9, FontStyle.Bold)
            };

            var linkLabel = new LinkLabel
            {
                Text = "▶ API 키 발급받는 곳 (클릭)",
                Location = new Point(20, 45),
                AutoSize = true
            };
            linkLabel.LinkClicked += (s, e) => Process.Start(new ProcessStartInfo(AppConstants.API_KEY_PAGE_URL) { UseShellExecute = true });

            txtApiKey = new TextBox
            {
                Location = new Point(20, 72),
                Width = 460,
                Text = _keyStore.Load() ?? ""   // 재입력 시 기존 키를 채워서 표시
            };

            var btnSave = new Button
            {
                Text = "저장",
                Location = new Point(380, 105),
                Size = new Size(100, 30)
            };
            btnSave.Click += BtnSave_Click;

            this.AcceptButton = btnSave;
            this.Controls.Add(lblInfo);
            this.Controls.Add(linkLabel);
            this.Controls.Add(txtApiKey);
            this.Controls.Add(btnSave);
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string key = txtApiKey.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("API 키를 입력해주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _keyStore.Save(key);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"API 키를 저장하지 못했습니다.\n{ex.Message}", "저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ApiKey = key;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}