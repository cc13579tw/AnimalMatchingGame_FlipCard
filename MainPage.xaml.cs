using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace AnimalMatchingGame
{
    public partial class MainPage : ContentPage
    {
        private int pairCount;
        private const int TimeLimitSeconds = 60;
        private const string CardBack = "?";
        private const string BestTimeKey = "BestTimeSeconds";

        //多組動物池，每局會先抽一組，再從那組抽 8 種
        private static readonly string[][] AnimalPools =
        {
            new[] { "🦁","🐶","🤖","🦊","🦝","🐨","🦄","🐮","🐷","🐸","🐵","🐔"  },
            new[] { "🐙","🦈","🐳","🐬","🦀","🦞","🦐","🐠","🐟","🦑","🐡","🦭"  },
            new[] { "🦉","🦅","🦇","🦜","🐧","🦢","🦩","🐦","🦤","🐤","🐣","🐚"  }
        };

        //Timer and game state
        private int tenthsElapsed = 0;
        private int tenthsRemaining = TimeLimitSeconds * 10;
        private bool timerRunning = false;

        private Button? lastClicked = null;     //第一張翻開的牌
        private bool findingMatch = false;      //false: 等第一張，true: 等第二張
        private int matchesFound = 0;           //以配對成功的對數
        private bool isResolving = false;       //防止第二下處理時被連點

        public MainPage()
        {
            InitializeComponent();
            LoadBestTime();
        }

        private void LoadBestTime()
        {
            double best = Preferences.Get(BestTimeKey, -1d);
            BestTimeLabel.Text = best > 0 ? $"Best: {best:0.0}s" : "Best: --";
        }

        private void StartNewGame()
        {
            pairCount = AnimalButtons.Children.OfType<Button>().Count() / 2;

            matchesFound = 0;
            findingMatch = false;
            lastClicked = null;
            isResolving = false;

            tenthsElapsed = 0;
            tenthsRemaining = TimeLimitSeconds * 10;

            AnimalButtons.IsVisible = true;
            PlayAgainButton.IsVisible = false;

            AssignCards();
            UpdateTimeLabels();

            if (!timerRunning)
            {
                timerRunning = true;
                Dispatcher.StartTimer(TimeSpan.FromSeconds(0.1), TimerTick);
            }
        }

        private void AssignCards()
        {
            // 1) 隨機選一組 pool
            string[] pool = AnimalPools[Random.Shared.Next(AnimalPools.Length)];

            // 2) 從 pool 抽 8 個不同動物
            List<string> chosen = pool
                .OrderBy(_ => Random.Shared.Next())
                .Take(pairCount)
                .ToList();

            // 3) 做成 8 對，再洗牌
            List<string> deck = chosen
                .SelectMany(a => new[] { a, a })
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            // 4) 填到按鈕，預設顯示牌背
            int i = 0;
            foreach (Button button in AnimalButtons.Children.OfType<Button>())
            {
                button.BindingContext = deck[i];
                button.Text = CardBack;
                button.IsEnabled = true;
                button.Background = new SolidColorBrush(Colors.LightBlue);
                i++;
            }
        }

        private void UpdateTimeLabels()
        {
            TimeElapsed.Text = $"Time Elapsed: {(tenthsElapsed / 10f):0.0}s";
            TimeRemainingLabel.Text = $"Time Remaining: {(tenthsRemaining / 10f):0.0}s";
        }

        private bool TimerTick()
        {
            if (!this.IsLoaded) return false;

            // 遊戲不在進行中就停表
            if (PlayAgainButton.IsVisible)
            {
                timerRunning = false;
                return false;
            }

            tenthsElapsed++;
            tenthsRemaining--;
            UpdateTimeLabels();

            if (tenthsRemaining <= 0)
            {
                EndGameLose();
                timerRunning = false;
                return false;
            }

            return true;
        }

        private void EndGameLose()
        {
            PlayAgainButton.IsVisible = true;
            AnimalButtons.IsVisible = false;

            DisplayAlert("Time's up", "You ran out of time.", "OK");
        }

        private void EndGameWin()
        {
            PlayAgainButton.IsVisible = true;
            AnimalButtons.IsVisible = false;

            double timeUsedSeconds = tenthsElapsed / 10.0;
            double best = Preferences.Get(BestTimeKey, -1d);

            if (best <= 0 || timeUsedSeconds < best)
            {
                Preferences.Set(BestTimeKey, timeUsedSeconds);
                BestTimeLabel.Text = $"Best: {timeUsedSeconds:0.0}s";
                DisplayAlert("New record", $"New best time: {timeUsedSeconds:0.0}s", "OK");
            }
            else
            {
                DisplayAlert("Completed", $"Time: {timeUsedSeconds:0,0}s", "OK");
            }
        }

        private void Reveal(Button button)
        {
            if (button.BindingContext is string emoji)
            {
                button.Text = emoji;
                button.Background = new SolidColorBrush(Colors.Orange);
            }
        }

        private void Hide(Button button)
        {
            button.Text = CardBack;
            button.Background = new SolidColorBrush(Colors.LightBlue);
        }

        private bool IsFaceUp(Button button)
        {
            return button.Text != CardBack && !string.IsNullOrEmpty(button.Text);
        }

        private bool IsMatched(Button button)
        {
            return !button.IsEnabled;
        }
        private void MarkMatched(Button a, Button b)
        {
            a.IsEnabled = false;
            b.IsEnabled = false;

            a.Background = new SolidColorBrush(Colors.LightBlue);
            b.Background = new SolidColorBrush(Colors.LightBlue);
        }

        private void PlayAgainButton_Clicked(object sender, EventArgs e)
        {
            StartNewGame();
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            if (isResolving) return;

            if (sender is not Button buttonClicked) return;
            if (IsMatched(buttonClicked)) return;
            if (IsFaceUp(buttonClicked)) return;    // 翻開就不重複翻

            // 第一張
            if (!findingMatch)
            {
                Reveal(buttonClicked);
                lastClicked = buttonClicked;
                findingMatch = true;
                return;
            }

            // 第二張
            if (lastClicked is null)
            {
                findingMatch = false;
                return;
            }

            isResolving = true;
            Reveal(buttonClicked);

            string a = lastClicked.BindingContext as string ?? "";
            string b = buttonClicked.BindingContext as string ?? "";

            // 相同且不是同一類
            if (buttonClicked != lastClicked && a == b)
            {
                MarkMatched(lastClicked, buttonClicked);
                matchesFound++;

                findingMatch = false;
                lastClicked = null;
                isResolving = false;

                if (matchesFound == pairCount)
                {
                    EndGameWin();
                }

                return;
            }

            //不相同，延遲蓋回去
            await Task.Delay(500);

            Hide(lastClicked);
            Hide(buttonClicked);

            findingMatch = false;
            lastClicked = null;
            isResolving = false;
        }
    }
}

        /*
        private void PlayAgainButton_Clicked(object sender, EventArgs e)
        {//sender 是觸發事件的物件（這裡是 PlayAgainButton）。EventArgs e 是事件參數。
            AnimalButtons.IsVisible = true;
            PlayAgainButton.IsVisible = false;

            // 新語法
            // List<string> animalEmoji = ["🦁", "🐶"];
            List<string> animalEmoji = new List<string>
            {
                "🦁","🦁",
                "🐶","🐶",
                "🤖","🤖",
                "🦊","🦊",
                "🦝","🦝",
                "🐨","🐨",
                "🦄","🦄",
                "🐮","🐮",
            };
            //逐一尋覽集合。AnimalButtons.Children 是 FlexLayout 的子控制項集合。
            //.OfType<Button>() 是 LINQ 方法，篩選出型別是 Button 的元素。
            //var button 是迴圈內的 區域變數 local variable，代表目前這顆按鈕
            foreach (var button in AnimalButtons.Children.OfType<Button>())
            {   //int 是型別，index 是變數。Random.Shared 是共用亂數產生器。
                //.Next(animalEmoji.Count) 產生 0 到 Count-1 的隨機整數。
                int index = Random.Shared.Next(animalEmoji.Count);
                string nextEmoji = animalEmoji[index];//取出該索引的 emoji 字串，存到 nextEmoji 變數。
                button.Text = nextEmoji;//把 emoji 顯示到按鈕上。
                animalEmoji.RemoveAt(index);
                //從清單移除已使用的 emoji，避免重複分配到其他按鈕，確保每個 emoji 只用一次。
            }
            // 啟動計時器，並且每 0.1 秒執行一次名為 TimerTick 的方法
            Dispatcher.StartTimer(TimeSpan.FromSeconds(.1), TimerTick);
        }
        // 新增 0.1 秒計時變數的欄位
        int tenthsOfSecondsElapsed = 0;
        private bool TimerTick()// 這個方法總共有七個陳述式
        {
            if (!this.IsLoaded) return false; //1. 計時器可能在關閉app後繼續跳動，導致錯誤。這個陳述式可防止這種情況
            tenthsOfSecondsElapsed++;//2. 將這個欄位的值加上 1 用於記錄過了多少個 0.1 秒。
            //3. 這個陳述式會更新 TimeElapsed 標籤讓它顯示最新時間。
            //它會將 0.1 秒的數量除以 10，將時間轉換為秒數。
            TimeElapsed.Text = "Time elapsed: " + (tenthsOfSecondsElapsed / 10F).ToString("0.0s");
            //4. 如果 Play Again 按鈕再次顯示代表遊戲已經結束，計時器可以停止運作
            if (PlayAgainButton.IsVisible)
            {
                tenthsOfSecondsElapsed = 0;//5. 重設計時器
                return false;//6. 讓計時器停止，並且不會執行方法中其他陳述式
            }
            return true;//7. 只會在 if 陳述式沒有發現 Play Again 按鈕被顯示出來時執行，讓計時器運行
        }

        Button? lastClicked; //記錄「第一下按鈕」
        bool findingMatch = false; //false 表示正在等「第一下」, true 表示正在等「第二下」
        int matchedFound; // 記錄已成功配對的「對數」

        private void Button_Clicked(object sender, EventArgs e)
        {   //型別判斷與轉型 //若點擊來源是按鈕，把 sender 轉成 Button 並存到區域變數 buttonClicked。
            if (sender is Button buttonClicked)
            {   //條件 1：buttonClicked.Text 不是 null、不是空字串、也不是只有空白。
                //條件 2：findingMatch == false 代表目前在等「第一下」。
                if (!string.IsNullOrWhiteSpace(buttonClicked.Text) && (findingMatch == false))
                {   //Background 是 屬性 property，型別是 Brush。
                    //SolidColorBrush 是 型別 type，用單色填滿背景。Colors.Orange 是 顏色值
                    buttonClicked.Background = new SolidColorBrush(Colors.Orange);
                    lastClicked = buttonClicked;//記錄第一下按鈕到 lastClicked 欄位，供第二下比對。
                    findingMatch = true;//狀態切換成等待第二下。
                }
                else//處理「第二下」
                {   //條件 1：buttonClicked != lastClicked，避免同一顆按兩次就算配對。
                    //條件 2：buttonClicked.Text == lastClicked.Text，兩顆顯示的 emoji 相同。
                    //條件 3：第二下按的那顆不是空白（避免點到已清空的按鈕）。

                    if (lastClicked is null)
                    {   // 理論上不該發生，但可保護避免偶發狀態造成 NullReferenceException
                        findingMatch = false;
                        return;
                    }

                    if ((buttonClicked != lastClicked) && (buttonClicked.Text == lastClicked.Text)
                        && !string.IsNullOrWhiteSpace(buttonClicked.Text))
                    {   //配對成功的話
                        matchedFound++;
                        lastClicked.Text = " ";
                        buttonClicked.Text = " ";
                        lastClicked.Background = new SolidColorBrush(Colors.LightBlue);
                        buttonClicked.Background = new SolidColorBrush(Colors.LightBlue);
                    }
                    else
                    {   //配對不成功背景色還原黑色
                        lastClicked.Background = new SolidColorBrush(Colors.Black);
                        buttonClicked.Background = new SolidColorBrush(Colors.Black);
                    }
                    findingMatch = false;//不論配對成功或失敗，第二下處理結束後都回到等待第一下狀態。
                }
            }
            if (matchedFound == 8)
            {
                matchedFound = 0;
                AnimalButtons.IsVisible = false;
                PlayAgainButton.IsVisible = true;
            }
        }
    }
}
*/
