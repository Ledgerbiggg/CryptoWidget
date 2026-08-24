# OKX WebSocket 行情订阅接口文档（桌面版软件用）

> 用途：本文件提炼自 `ledger-service` 项目中 OKX 行情订阅模块的核心逻辑，
> 供 AI 据此生成一个**桌面版 OKX 行情订阅工具**。
> 软件需支持：用户可视化配置要订阅的币种/频道；常驻桌面持续接收 WebSocket 推送；
> 在桌面（如悬浮窗 / 托盘 / 顶栏）实时跳动显示行情。
>
> 代理说明：**本软件不需要配置代理**。用户电脑上已开启系统/全局代理，
> 程序直接走系统网络环境即可连接 OKX（代码默认使用系统环境变量代理）。

---

## 1. 连接信息

| 项 | 值 |
| --- | --- |
| WebSocket 地址 | `wss://ws.okx.com:8443/ws/v5/public` |
| 协议 | OKX 公共行情 WS v5（无需登录鉴权） |
| 数据格式 | JSON（文本帧） |
| 心跳 | OKX 公共频道无需客户端发心跳，但需处理服务端断线重连 |

> 注：这是 OKX **公共行情**频道，订阅 `tickers`、`books`、`trades`、`candle*` 等都走此地址，无需 API Key。

---

## 2. 订阅请求（客户端 → 服务端）

连接建立后，客户端发送一条订阅消息：

```json
{
  "op": "subscribe",
  "args": [
    { "channel": "tickers", "instId": "BTC-USDT" },
    { "channel": "tickers", "instId": "ETH-USDT" }
  ]
}
```

- `op`：固定为 `"subscribe"`（取消订阅为 `"unsubscribe"`，结构相同）
- `args`：订阅项数组，至少 1 项
- 每项的字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `channel` | string | 频道名，见第 3 节 |
| `instId` | string | 交易对 / 产品 ID，如 `BTC-USDT`、`ETH-USDT`、`BTC-USD-SWAP` |

支持一次订阅多个频道/币种，合并在 `args` 里发送即可。

### 常见频道 `channel` 取值

| channel | 含义 |
| --- | --- |
| `tickers` | 行情 tick，包含最新价、买卖盘、24h 高低、成交量 |
| `books` | 订单簿（深度） |
| `books5` | 五档深度 |
| `trades` | 逐笔成交 |
| `candle1m` / `candle5m` / `candle1H` / `candle1D` | K 线（不同周期） |

> 桌面版推荐默认使用 `tickers` 频道（实时跳动价格最合适）。

---

## 3. 推送消息结构（服务端 → 客户端）

### 3.1 tickers 频道完整结构

```json
{
  "arg": {
    "channel": "tickers",
    "instId": "BTC-USDT"
  },
  "data": [
    {
      "instType": "SPOT",
      "instId": "BTC-USDT",
      "last": "65000.1",
      "lastSz": "0.001",
      "askPx": "65001.0",
      "askSz": "0.5",
      "bidPx": "65000.0",
      "bidSz": "0.3",
      "open24h": "64000.0",
      "high24h": "65500.0",
      "low24h": "63800.0",
      "sodUtc0": "64200.0",
      "sodUtc8": "64300.0",
      "volCcy24h": "123456789.0",
      "vol24h": "1900.5",
      "ts": "1718000000000"
    }
  ]
}
```

> 注意：`data` 中**所有数值字段都是字符串类型**（OKX 设计如此），前端/客户端需自行 `parseFloat` / `parseInt`。

### 3.2 字段含义表（tickers）

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `arg.channel` | string | 回显：频道名 |
| `arg.instId` | string | 回显：交易对 |
| `data[].instType` | string | 产品类型：`SPOT` / `SWAP` / `FUTURES` / `OPTION` |
| `data[].instId` | string | 产品 ID |
| `data[].last` | string | 最新成交价 |
| `data[].lastSz` | string | 最新成交数量 |
| `data[].askPx` | string | 卖一价 |
| `data[].askSz` | string | 卖一量 |
| `data[].bidPx` | string | 买一价 |
| `data[].bidSz` | string | 买一量 |
| `data[].open24h` | string | 24h 开盘价 |
| `data[].high24h` | string | 24h 最高价 |
| `data[].low24h` | string | 24h 最低价 |
| `data[].sodUtc0` | string | UTC+0 当日开盘价 |
| `data[].sodUtc8` | string | UTC+8 当日开盘价 |
| `data[].volCcy24h` | string | 24h 成交额（计价币） |
| `data[].vol24h` | string | 24h 成交量（基础币） |
| `data[].ts` | string | 推送时间戳（毫秒，字符串） |

### 3.3 24h 涨跌幅计算

OKX 不直接下发涨跌幅，需本地计算：

```
change24h% = (parseFloat(last) - parseFloat(open24h)) / parseFloat(open24h) * 100
```

涨跌方向用于桌面跳动配色（涨绿/跌红，或按用户地区习惯）：
- `last > open24h` → 涨
- `last < open24h` → 跌
- 相等 → 平

### 3.4 其他控制类消息

连接或订阅成功/失败时，OKX 会回一条事件消息（**没有 `data` 字段**）：

```json
{ "event": "subscribe", "arg": { "channel": "tickers", "instId": "BTC-USDT" } }
```

或错误：

```json
{ "event": "error", "code": "600xx", "msg": "..." }
```

> 客户端收到消息后，**先判断是否有 `data` 字段**：有 `data` 才是行情；否则是事件/错误回执。

---

## 4. 客户端必须实现的逻辑（核心）

以下逻辑来自原项目的 `TickerClient`，桌面版需等价实现：

### 4.1 内部状态缓存（用于涨跌方向判断）

为每个订阅币种维护一个状态对象，记录「上一次价格」与「当前价格」，
用来判断本次推送是涨还是跌（用于桌面跳动颜色/动画）：

```
SymbolPriceState {
    Symbol:      instId
    LastPrice:   上一次价格
    LastTime:    上一次时间
    CurPrice:    当前价格
    CurTime:     当前时间
}
```

每次收到 `tickers` 消息时：
1. 把 `CurPrice` 移到 `LastPrice`
2. 把新 `last` 写入 `CurPrice`
3. 比较 `CurPrice` 与 `LastPrice` / `open24h` 决定方向

### 4.2 读取循环 + 断线重连（关键）

- 用独立线程/循环持续 `ReadMessage`
- 一旦读取报错（连接断开），**立即触发重连**
- 重连逻辑（指数退避）：

```
backoff = 1s
loop:
    建立新 WS 连接
    重新 subscribe(当前所有订阅)
    if 成功: 退出循环
    else:
        等待 backoff
        backoff = min(backoff * 2, 30s)
        重试
```

- 重连时要**先关闭旧连接、再建新连接、再重新订阅**
- 用布尔锁（`reconnecting`）防止并发重复重连

### 4.3 动态重载订阅（用户改配置时）

用户在 UI 上增删币种/频道后，不需要重启程序，应：
1. 停止当前读取循环
2. 关闭旧连接
3. 建立新连接
4. 用「新订阅列表」重新 `subscribe`
5. 清理已取消币种的缓存状态
6. 启动新读取循环

> 等价原项目 `TickerClient.Reload()` 行为。

### 4.4 代理处理（桌面版无需配置）

- 程序默认使用**系统网络环境**连接（即自动走用户电脑已开启的代理）
- 不应在软件里写死代理地址；若使用 `http` 类库，使用其「从环境变量读取代理」的默认行为即可
- 除非用户明确在设置里填了代理地址，否则不要覆盖系统代理

---

## 5. 桌面版软件功能建议

| 模块 | 说明 |
| --- | --- |
| 订阅配置 | 用户可增删 `instId`（如 BTC-USDT）；可选频道（默认 tickers）；可保存为本地配置（JSON/SQLite） |
| 常驻显示 | 悬浮窗 / 系统托盘 + 顶栏小条，持续显示最新价并随推送跳动 |
| 实时跳动 | 收到推送时价格数字闪烁 / 涨跌色变化 / 轻微动画 |
| 连接状态 | 显示 WS 已连接 / 重连中；断线自动重连不阻断 UI |
| 多币种列表 | 同时订阅多个币种，列表实时刷新；可显示 24h 涨跌幅 |
| 本地持久化 | 订阅列表存本地，重启后自动恢复订阅 |

---

## 6. 原项目关键代码位置（参考）

| 文件 | 作用 |
| --- | --- |
| `src/services/web/services/thirdparty/t_okx_ticker.go` | WS 连接、订阅、读循环、重连、状态缓存、重载 |
| `src/services/web/services/thirdparty/t_okx_candles.go` | K 线 REST 接口（桌面版可选，非 WS） |
| `test/okx_test.go` | WS 连接 + 订阅 + 读取的最小可用示例（可直接抄） |
| `src/config/config.go` | OKX 代理配置项（桌面版可忽略，走系统代理） |

### 最小可用示例（来自 `test/okx_test.go`，伪代码）

```
wsURL = "wss://ws.okx.com:8443/ws/v5/public"
conn = dial(wsURL)                        // 默认走系统代理
conn.write({
  "op": "subscribe",
  "args": [{ "channel": "tickers", "instId": "BTC-USDT" }]
})
loop:
  msg = conn.read()
  if msg has "data":
      解析 tickers，更新 UI 跳动
  else:
      忽略事件回执
```

---

## 7. 给 AI 的构建提示

- 技术栈不限（Electron / Tauri / WPF / Qt / Flutter 桌面 均可），重点是上述 WS 协议与逻辑
- 务必实现：**系统代理直连、断线指数退避重连、动态重载订阅、数值字符串解析、24h 涨跌幅本地计算、状态缓存判涨跌**
- 桌面跳动体验是核心卖点，推送频率高（tickers 约每秒多次），UI 更新需做节流/批量
- 不需要任何 OKX API Key 或登录
