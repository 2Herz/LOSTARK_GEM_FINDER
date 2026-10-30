using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using LOSTARK_GEM_FINDER.Core;
using LOSTARK_GEM_FINDER.Models;

namespace LOSTARK_GEM_FINDER.Views
{
    /// <summary>
    /// 메인 화면. 디자이너 없이 코드로만 UI를 구성합니다.
    /// 왼쪽: 레벨·종류별 최저가 표 / 오른쪽 위: 목표가 알림 / 오른쪽 아래: 시세 기록 그래프
    /// </summary>
    public class MainForm : Form
    {
        #region 📌 1. 필드 및 초기화
        private const int COL_LEVEL = 0;
        private const int ALARM_COL_GEM = 0;
        private const int ALARM_COL_TARGET = 1;
        private const int ALARM_COL_CURRENT = 2;
        private const int ALARM_COL_STATE = 3;

        private const int PRICE_GRID_PERCENT = 45;
        private const int ALARM_AREA_PERCENT = 40;
        private const int FULL_PERCENT = 100;
        private const int TARGET_PRICE_INCREMENT = 10000;
        private const int MAX_TARGET_PRICE = 100000000;
        private const int CHART_LINE_WIDTH = 3;
        private const int CHART_MARKER_SIZE = 7;

        private static readonly Size FORM_SIZE = new Size(1100, 680);
        private static readonly Size MIN_FORM_SIZE = new Size(900, 560);
        private static readonly Font TITLE_FONT = new Font("맑은 고딕", 11, FontStyle.Bold);

        private readonly IGemPriceService _service;
        private readonly IAuctionAPI _api;
        private readonly IAPIKeyStore _keyStore;
        private readonly IGemDataRepository _db;

        private Button btnRefresh;
        private CheckBox chkAutoRefresh;
        private Label lblStatus;
        private DataGridView gridPrices;
        private ComboBox cboAlarmGem;
        private NumericUpDown numTargetPrice;
        private DataGridView gridAlarms;
        private ComboBox cboChartGem;
        private Chart chartHistory;

        private Timer autoTimer;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;

        private bool isRefreshing = false;
        private bool isChangingKey = false;

        public MainForm(IGemPriceService service, IAuctionAPI api, IAPIKeyStore keyStore, IGemDataRepository db)
        {
            _service = service;
            _api = api;
            _keyStore = keyStore;
            _db = db;

            this.Text = "로아 보석 최저가 검색기";
            this.Size = FORM_SIZE;
            this.MinimumSize = MIN_FORM_SIZE;
            this.StartPosition = FormStartPosition.CenterScreen;

            BuildLayout();
            InitializeTrayIcon();

            autoTimer = new Timer { Interval = AppConstants.AUTO_REFRESH_INTERVAL_MS };
            autoTimer.Tick += AutoTimer_Tick;

            _service.OnPricesUpdated += RefreshUI;
            this.Load += MainForm_Load;
            this.Resize += MainForm_Resize;
            this.FormClosed += MainForm_FormClosed;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            RefreshUI();

            if (!string.IsNullOrEmpty(_db.LoadWarning))
                MessageBox.Show(_db.LoadWarning, "저장 파일 복구", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            autoTimer.Stop();
            autoTimer.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayMenu.Dispose();
        }
        #endregion

        #region 🎨 2. 화면 구성
        private void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, FULL_PERCENT));
            root.Controls.Add(BuildTopBar(), 0, 0);

            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, PRICE_GRID_PERCENT));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, FULL_PERCENT - PRICE_GRID_PERCENT));
            content.Controls.Add(BuildPriceGroup(), 0, 0);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            right.RowStyles.Add(new RowStyle(SizeType.Percent, ALARM_AREA_PERCENT));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, FULL_PERCENT - ALARM_AREA_PERCENT));
            right.Controls.Add(BuildAlarmGroup(), 0, 0);
            right.Controls.Add(BuildChartGroup(), 0, 1);
            content.Controls.Add(right, 1, 0);

            root.Controls.Add(content, 0, 1);
            this.Controls.Add(root);
        }

        private Control BuildTopBar()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };

            btnRefresh = new Button { Text = "시세 갱신", AutoSize = true };
            btnRefresh.Click += BtnRefresh_Click;

            chkAutoRefresh = new CheckBox
            {
                Text = $"자동 갱신 ({AppConstants.AUTO_REFRESH_MINUTES}분)",
                AutoSize = true,
                Margin = new Padding(12, 7, 3, 3)
            };
            chkAutoRefresh.CheckedChanged += ChkAutoRefresh_CheckedChanged;

            lblStatus = new Label { Text = "아직 조회하지 않았어요.", AutoSize = true, Margin = new Padding(12, 8, 3, 3) };

            bar.Controls.Add(btnRefresh);
            bar.Controls.Add(chkAutoRefresh);
            bar.Controls.Add(lblStatus);
            return bar;
        }

        private Control BuildPriceGroup()
        {
            var group = new GroupBox { Text = "레벨·종류별 최저가 (즉시 구매가)", Dock = DockStyle.Fill };

            gridPrices = CreateReadOnlyGrid();
            gridPrices.Columns.Add("Level", "레벨");
            gridPrices.Columns[COL_LEVEL].FillWeight = FULL_PERCENT / 2;

            foreach (var type in AppConstants.GemTypes)
            {
                int index = gridPrices.Columns.Add(type.ShortName, $"{type.ShortName} 최저가");
                gridPrices.Columns[index].Tag = type;
                gridPrices.Columns[index].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            // 높은 레벨이 위로
            for (int level = AppConstants.MAX_GEM_LEVEL; level >= AppConstants.MIN_GEM_LEVEL; level--)
            {
                int rowIndex = gridPrices.Rows.Add($"{level}레벨");
                gridPrices.Rows[rowIndex].Tag = level;
            }

            gridPrices.CellClick += GridPrices_CellClick;
            group.Controls.Add(gridPrices);
            return group;
        }

        private Control BuildAlarmGroup()
        {
            var group = new GroupBox { Text = "목표가 알림", Dock = DockStyle.Fill };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, FULL_PERCENT));

            var inputBar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };

            cboAlarmGem = CreateGemComboBox();

            numTargetPrice = new NumericUpDown
            {
                Maximum = MAX_TARGET_PRICE,
                Increment = TARGET_PRICE_INCREMENT,
                ThousandsSeparator = true,
                Width = 120
            };

            var btnAdd = new Button { Text = "추가/변경", AutoSize = true };
            btnAdd.Click += BtnAddAlarm_Click;

            var btnRemove = new Button { Text = "삭제", AutoSize = true };
            btnRemove.Click += BtnRemoveAlarm_Click;

            inputBar.Controls.Add(cboAlarmGem);
            inputBar.Controls.Add(new Label { Text = "목표가", AutoSize = true, Margin = new Padding(6, 7, 0, 0) });
            inputBar.Controls.Add(numTargetPrice);
            inputBar.Controls.Add(btnAdd);
            inputBar.Controls.Add(btnRemove);

            gridAlarms = CreateReadOnlyGrid();
            gridAlarms.Columns.Add("Gem", "보석");
            gridAlarms.Columns.Add("Target", "목표가");
            gridAlarms.Columns.Add("Current", "현재 최저가");
            gridAlarms.Columns.Add("State", "상태");
            gridAlarms.Columns[ALARM_COL_TARGET].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            gridAlarms.Columns[ALARM_COL_CURRENT].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            gridAlarms.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridAlarms.CellClick += GridAlarms_CellClick;

            layout.Controls.Add(inputBar, 0, 0);
            layout.Controls.Add(gridAlarms, 0, 1);
            group.Controls.Add(layout);
            return group;
        }

        private Control BuildChartGroup()
        {
            var group = new GroupBox { Text = "시세 기록 (일별 최저가)", Dock = DockStyle.Fill };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, FULL_PERCENT));

            cboChartGem = CreateGemComboBox();
            cboChartGem.SelectedIndexChanged += (s, e) => RefreshChart();

            chartHistory = new Chart { Dock = DockStyle.Fill };
            var area = new ChartArea("Main");
            area.AxisY.LabelStyle.Format = "N0";
            area.AxisY.IsStartedFromZero = false;
            area.AxisX.MajorGrid.LineColor = Color.Gainsboro;
            area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
            chartHistory.ChartAreas.Add(area);
            chartHistory.Series.Add(new Series("최저가")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = CHART_LINE_WIDTH,
                Color = Color.MediumPurple,
                MarkerStyle = MarkerStyle.Circle,
                MarkerSize = CHART_MARKER_SIZE,
                ToolTip = "#VALX : #VALY{N0} 골드"
            });
            chartHistory.Titles.Add(new Title { Font = TITLE_FONT });

            layout.Controls.Add(cboChartGem, 0, 0);
            layout.Controls.Add(chartHistory, 0, 1);
            group.Controls.Add(layout);
            return group;
        }

        private static DataGridView CreateReadOnlyGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
                MultiSelect = false
            };
        }

        private static ComboBox CreateGemComboBox()
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            foreach (var type in AppConstants.GemTypes)
            {
                for (int level = AppConstants.MAX_GEM_LEVEL; level >= AppConstants.MIN_GEM_LEVEL; level--)
                    combo.Items.Add(new GemSelection(type.ShortName, level));
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            return combo;
        }

        private void InitializeTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("열기", null, (s, ev) => RestoreFromTray());
            trayMenu.Items.Add("API 키 변경", null, (s, ev) => ChangeApiKey());
            trayMenu.Items.Add("종료", null, (s, ev) => Application.Exit());

            trayIcon = new NotifyIcon { Text = "로아 보석 최저가 검색기", Icon = SystemIcons.Application, ContextMenuStrip = trayMenu, Visible = true };
            trayIcon.DoubleClick += (s, ev) => RestoreFromTray();
        }
        #endregion

        #region 🖼️ 3. 화면 갱신
        private void RefreshUI()
        {
            if (this.InvokeRequired) { this.Invoke(new Action(RefreshUI)); return; }

            RefreshPriceGrid();
            RefreshAlarmGrid();
            RefreshChart();

            if (_service.LastUpdated.HasValue)
                lblStatus.Text = $"마지막 갱신: {_service.LastUpdated.Value:HH:mm:ss}";
        }

        private void RefreshPriceGrid()
        {
            foreach (DataGridViewRow row in gridPrices.Rows)
            {
                int level = (int)row.Tag;
                foreach (DataGridViewColumn column in gridPrices.Columns)
                {
                    var type = column.Tag as GemType;
                    if (type == null) continue;

                    if (_service.Quotes.TryGetValue(GemKeys.Make(type.ShortName, level), out GemQuote quote))
                        FormatPriceCell(row.Cells[column.Index], quote);
                }
            }
        }

        private static void FormatPriceCell(DataGridViewCell cell, GemQuote quote)
        {
            if (!quote.Price.HasValue)
            {
                cell.Style.ForeColor = Color.Gray;
                cell.Value = quote.PreviousPrice.HasValue ? "매물 없음" : "-";
                return;
            }

            long price = quote.Price.Value;
            long diff = quote.Diff ?? 0;

            if (diff > 0) { cell.Style.ForeColor = Color.Red; cell.Value = $"{price:N0} (▲{diff:N0})"; }
            else if (diff < 0) { cell.Style.ForeColor = Color.Blue; cell.Value = $"{price:N0} (▼{Math.Abs(diff):N0})"; }
            else { cell.Style.ForeColor = Color.Black; cell.Value = $"{price:N0}"; }
        }

        private void RefreshAlarmGrid()
        {
            var selected = GetSelectedAlarm();
            gridAlarms.Rows.Clear();

            foreach (var alarm in _service.Alarms)
            {
                long? current = _service.Quotes.TryGetValue(alarm.GemKey, out GemQuote quote) ? quote.Price : null;

                int rowIndex = gridAlarms.Rows.Add(
                    alarm.DisplayName,
                    $"{alarm.TargetPrice:N0}",
                    current.HasValue ? $"{current.Value:N0}" : "-",
                    alarm.IsTriggered ? "✔ 도달" : "대기");

                var row = gridAlarms.Rows[rowIndex];
                row.Tag = alarm;
                if (alarm.IsTriggered) row.DefaultCellStyle.BackColor = Color.Honeydew;
                if (alarm == selected) row.Selected = true;
            }
        }

        private void RefreshChart()
        {
            var gem = cboChartGem.SelectedItem as GemSelection;
            var series = chartHistory.Series[0];
            series.Points.Clear();
            if (gem == null) return;

            var history = _service.GetHistory(gem.Key);
            foreach (var day in history)
                series.Points.AddXY(day.Key, day.Value);

            chartHistory.Titles[0].Text = history.Count > 0
                ? $"{gem} 최저가 추이"
                : $"{gem} — 아직 기록이 없어요";
        }

        private void NotifyAlarms(List<PriceAlarm> reached)
        {
            if (reached.Count == 0) return;

            var lines = reached.Select(a =>
            {
                long price = _service.Quotes[a.GemKey].Price ?? 0;
                return $"{a.DisplayName}: {price:N0} 골드 (목표 {a.TargetPrice:N0})";
            });

            ShowBalloon("💎 목표가 도달", string.Join(Environment.NewLine, lines), ToolTipIcon.Info);
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            trayIcon.BalloonTipTitle = title;
            trayIcon.BalloonTipText = text;
            trayIcon.BalloonTipIcon = icon;
            trayIcon.ShowBalloonTip(AppConstants.BALLOON_TIP_MS);
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }
        #endregion

        #region 📌 4. 사용자 입력 및 이벤트
        private async void BtnRefresh_Click(object sender, EventArgs e) => await RefreshPricesAsync(false);

        // 🌟 PerformClick 대신 직접 호출 → 트레이로 숨겨져 있어도 갱신됨
        private async void AutoTimer_Tick(object sender, EventArgs e) => await RefreshPricesAsync(true);

        private async void ChkAutoRefresh_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAutoRefresh.Checked)
            {
                autoTimer.Start();
                await RefreshPricesAsync(true);
            }
            else autoTimer.Stop();
        }

        private async Task RefreshPricesAsync(bool isAuto)
        {
            if (isRefreshing || isChangingKey) return;
            isRefreshing = true;

            string keyErrorMessage = null;
            btnRefresh.Enabled = false;
            lblStatus.Text = "시세 조회 중...";
            try
            {
                var reached = await _service.RefreshAsync();
                NotifyAlarms(reached);
            }
            catch (ApiKeyInvalidException ex)
            {
                chkAutoRefresh.Checked = false;   // 잘못된 키로 계속 재시도하지 않도록 중지
                keyErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                if (isAuto) ShowBalloon("시세 자동 갱신 실패", ex.Message, ToolTipIcon.Error);
                else MessageBox.Show(ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnRefresh.Enabled = true;
                isRefreshing = false;
                lblStatus.Text = _service.LastUpdated.HasValue ? $"마지막 갱신: {_service.LastUpdated.Value:HH:mm:ss}" : "조회 실패";
            }

            // 🌟 isRefreshing이 풀리고 요청도 끝난 뒤에 키 재입력 안내
            if (keyErrorMessage == null) return;
            if (this.Visible) PromptApiKey(keyErrorMessage);
            else ShowBalloon("API 키 오류", keyErrorMessage, ToolTipIcon.Error);
        }

        private void PromptApiKey(string reason)
        {
            var answer = MessageBox.Show($"{reason}\n\nAPI 키를 다시 입력할까요?", "API 키 오류", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer == DialogResult.Yes) ChangeApiKey();
        }

        private void ChangeApiKey()
        {
            if (isRefreshing || isChangingKey)
            {
                MessageBox.Show("시세 조회 중에는 API 키를 변경할 수 없어요.\n잠시 후 다시 시도해 주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            isChangingKey = true;
            bool wasTimerRunning = autoTimer.Enabled;
            autoTimer.Stop();   // 모달 창이 떠 있는 동안 Tick이 발생하지 않도록 정지
            try
            {
                using (var keyForm = new APIKeyForm(_keyStore))
                {
                    if (keyForm.ShowDialog(this) != DialogResult.OK) return;
                    _api.SetApiKey(keyForm.ApiKey);
                }
            }
            finally
            {
                isChangingKey = false;
                if (wasTimerRunning) autoTimer.Start();
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState != FormWindowState.Minimized) return;

            this.Hide();
            string text = chkAutoRefresh.Checked
                ? $"{AppConstants.AUTO_REFRESH_MINUTES}분마다 시세를 확인하고, 목표가에 도달하면 알려드려요."
                : "트레이 아이콘을 더블클릭하면 다시 열립니다.";
            ShowBalloon("보석 검색기 백그라운드 실행 중", text, ToolTipIcon.Info);
        }

        // 최저가 표의 칸을 누르면 해당 보석을 그래프와 알림 입력칸에 바로 선택
        private void GridPrices_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var type = gridPrices.Columns[e.ColumnIndex].Tag as GemType;
            if (type == null) return;

            int level = (int)gridPrices.Rows[e.RowIndex].Tag;
            string key = GemKeys.Make(type.ShortName, level);
            SelectGem(cboChartGem, key);
            SelectGem(cboAlarmGem, key);

            if (_service.Quotes.TryGetValue(key, out GemQuote quote) && quote.Price.HasValue)
                numTargetPrice.Value = Math.Min(quote.Price.Value, MAX_TARGET_PRICE);
        }

        private void GridAlarms_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            var alarm = GetSelectedAlarm();
            if (alarm == null) return;

            SelectGem(cboAlarmGem, alarm.GemKey);
            SelectGem(cboChartGem, alarm.GemKey);
            numTargetPrice.Value = Math.Min(alarm.TargetPrice, MAX_TARGET_PRICE);
        }

        private void BtnAddAlarm_Click(object sender, EventArgs e)
        {
            var gem = cboAlarmGem.SelectedItem as GemSelection;
            long target = (long)numTargetPrice.Value;
            if (gem == null) return;
            if (target <= 0)
            {
                MessageBox.Show("목표가를 입력해 주세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try { _service.SetAlarm(gem, target); }
            catch (Exception ex) { ShowSaveError(ex); }
            RefreshAlarmGrid();
        }

        private void BtnRemoveAlarm_Click(object sender, EventArgs e)
        {
            var alarm = GetSelectedAlarm();
            if (alarm == null) return;

            try { _service.RemoveAlarm(alarm); }
            catch (Exception ex) { ShowSaveError(ex); }
            RefreshAlarmGrid();
        }

        private PriceAlarm GetSelectedAlarm()
            => gridAlarms.SelectedRows.Count > 0 ? gridAlarms.SelectedRows[0].Tag as PriceAlarm : null;

        private static void SelectGem(ComboBox combo, string gemKey)
        {
            var match = combo.Items.Cast<GemSelection>().FirstOrDefault(g => g.Key == gemKey);
            if (match != null) combo.SelectedItem = match;
        }

        private static void ShowSaveError(Exception ex)
            => MessageBox.Show($"저장에 실패했습니다.\n{ex.Message}", "저장 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        #endregion
    }
}