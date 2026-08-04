using NestFlow_Backend.Common;
using NestFlow_Backend.Helpers;
using NestFlow_Backend.Services.External;

namespace NestFlow_Backend.Tests;

/// <summary>
/// DifyResultMapper 是純函數，直接測試各種未經驗證的 Dify 輸出如何被轉換或拒絕，
/// 不需要啟動整個 API。
/// </summary>
public class DifyResultMapperTests
{
    private const decimal MinConfidence = 0.6m;

    [Fact]
    public void 記帳結果_信心足夠且分類存在時_應轉成FixedCommand()
    {
        var result = new DifyCommandResult
        {
            Kind = "entry",
            Type = "expense",
            Amount = 180,
            Category = "food",
            Note = "牛肉麵",
            Confidence = 0.98m,
        };

        var command = DifyResultMapper.ToFixedCommand(result, MinConfidence);

        Assert.NotNull(command);
        Assert.Equal(FixedCommandKind.Entry, command!.Kind);
        Assert.Equal(EntryType.Expense, command.Type);
        Assert.Equal(180m, command.Amount);
        Assert.Equal("food", command.Category);
        Assert.Equal("牛肉麵", command.Note);
        Assert.False(command.CategoryIsFallback);
    }

    [Fact]
    public void 記帳結果_分類代碼未知時_不應信任_應歸為其他()
    {
        var result = new DifyCommandResult
        {
            Kind = "entry",
            Type = "expense",
            Amount = 100,
            Category = "not-a-real-category",
            Confidence = 0.9m,
        };

        var command = DifyResultMapper.ToFixedCommand(result, MinConfidence);

        Assert.NotNull(command);
        Assert.Equal("other_expense", command!.Category);
        Assert.True(command.CategoryIsFallback);
    }

    [Fact]
    public void 記帳結果_分類與收支類型不符時_應歸為其他()
    {
        // "salary" 是收入分類，用在支出上應視為未知分類
        var result = new DifyCommandResult
        {
            Kind = "entry",
            Type = "expense",
            Amount = 100,
            Category = "salary",
            Confidence = 0.9m,
        };

        var command = DifyResultMapper.ToFixedCommand(result, MinConfidence);

        Assert.Equal("other_expense", command!.Category);
        Assert.True(command.CategoryIsFallback);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    public void 記帳結果_金額不是正數時_應回傳null(double amount)
    {
        var result = new DifyCommandResult
        {
            Kind = "entry",
            Type = "expense",
            Amount = (decimal)amount,
            Category = "food",
            Confidence = 0.9m,
        };

        Assert.Null(DifyResultMapper.ToFixedCommand(result, MinConfidence));
    }

    [Fact]
    public void 記帳結果_沒有金額時_應回傳null()
    {
        var result = new DifyCommandResult
        {
            Kind = "entry",
            Type = "expense",
            Amount = null,
            Category = "food",
            Confidence = 0.9m,
        };

        Assert.Null(DifyResultMapper.ToFixedCommand(result, MinConfidence));
    }

    [Fact]
    public void 信心值低於門檻時_應回傳null()
    {
        var result = new DifyCommandResult
        {
            Kind = "entry",
            Type = "expense",
            Amount = 100,
            Category = "food",
            Confidence = 0.2m,
        };

        Assert.Null(DifyResultMapper.ToFixedCommand(result, MinConfidence));
    }

    [Fact]
    public void Kind為none時_應回傳null()
    {
        var result = new DifyCommandResult { Kind = "none", Confidence = 0.9m };

        Assert.Null(DifyResultMapper.ToFixedCommand(result, MinConfidence));
    }

    [Fact]
    public void 行程結果_相對日期且有起訖時間時_應轉成FixedCommand()
    {
        var result = new DifyCommandResult
        {
            Kind = "event",
            Confidence = 0.95m,
            Event = new DifyEventResult
            {
                Title = "跟教授討論論文",
                DayOffset = 1,
                StartHour = 14,
                StartMinute = 0,
                EndHour = 15,
                EndMinute = 30,
            },
        };

        var command = DifyResultMapper.ToFixedCommand(result, MinConfidence);

        Assert.NotNull(command);
        Assert.Equal(FixedCommandKind.Event, command!.Kind);
        Assert.Equal("跟教授討論論文", command.Event!.Title);
        Assert.Equal(1, command.Event.DayOffset);
        Assert.Equal(14, command.Event.StartHour);
        Assert.Equal(15, command.Event.EndHour);
    }

    [Fact]
    public void 行程結果_同時有明確日期與相對日期時_應回傳null()
    {
        var result = new DifyCommandResult
        {
            Kind = "event",
            Confidence = 0.9m,
            Event = new DifyEventResult
            {
                Title = "開會",
                Month = 8,
                Day = 10,
                DayOffset = 1,
                StartHour = 14,
                StartMinute = 0,
            },
        };

        Assert.Null(DifyResultMapper.ToFixedCommand(result, MinConfidence));
    }

    [Fact]
    public void 行程結果_缺少開始時間時_應回傳null()
    {
        var result = new DifyCommandResult
        {
            Kind = "event",
            Confidence = 0.9m,
            Event = new DifyEventResult { Title = "開會", DayOffset = 0 },
        };

        Assert.Null(DifyResultMapper.ToFixedCommand(result, MinConfidence));
    }

    [Fact]
    public void 行程結果_只給結束小時沒給結束分鐘時_應回傳null()
    {
        var result = new DifyCommandResult
        {
            Kind = "event",
            Confidence = 0.9m,
            Event = new DifyEventResult
            {
                Title = "開會",
                DayOffset = 0,
                StartHour = 14,
                StartMinute = 0,
                EndHour = 15,
                EndMinute = null,
            },
        };

        Assert.Null(DifyResultMapper.ToFixedCommand(result, MinConfidence));
    }

    [Fact]
    public void 行程結果_標題空白時_應回傳null()
    {
        var result = new DifyCommandResult
        {
            Kind = "event",
            Confidence = 0.9m,
            Event = new DifyEventResult { Title = "  ", DayOffset = 0, StartHour = 14, StartMinute = 0 },
        };

        Assert.Null(DifyResultMapper.ToFixedCommand(result, MinConfidence));
    }

    [Fact]
    public void 結果為null時_應回傳null()
    {
        Assert.Null(DifyResultMapper.ToFixedCommand(null, MinConfidence));
    }
}
