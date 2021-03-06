# K-分布式 tracker 网络技术规范

<span style="text-align:right">版本 1.2c<br/>2015年1月30日<br/>by micstu@FGBT/蟹床</span>

------

## I 组成

- 客户端之间的通信
- 客户端与 BitTorrent 客户端之间的通信

## II 目的

实现运行于内网的分布式 tracker 服务器。即，将 tracker 信息保留在多台主机上，并使这些主机能共享信息，实现基本无中心的 tracker 服务网络。

*目前，本网络运行在接力模式下。即，消息传递使用接力的方式，而且不保证消息的完全送达。*

## III 功能

### 3.0 基础

#### 3.0.1 功能分类

现在实现的功能如下。

- 客户端之间的通信
- 本机 tracker 服务器与 BitTorrent 客户端（例如 μTorrent）之间的通信
- 本机 tracker 服务器与客户端之间的通信

舍弃的功能如下。

- 与 BitTorrent 客户端之间通过 WebUI 的交互。*（延时性差，异常捕获难。）*
- 本地数据库缓存。*（不能保证主机安装了数据库驱动程序。）*

#### 3.0.2 用词解说

- 连接列表。某个客户端自身保存着的，可以从这一点连接到的客户端终端/端点（endpoint）列表。
- 客户端（client）。若无其他描述，默认为本软件的客户端。
- 节点（node）。表示分布网络中的一个可以与其他单位交流的单位，通常和一个客户端是等价的。
- 分布网络。本软件集群的通信网络。帮助中的用语为 KS 网络（KeiSystem 网络）。
- 用户（peer）。表示 μTorrent 的一个实例，该实例拥有一个指定 infohash 的种子文件的文件。
- 主机。客户端运行在的计算机。
- InfoHash。BitTorrent 客户端用来区别种子的20位 SHA-1 散列字节数组。其字符串表示形式为16进制字符（0-9,A-F）表示的字节数组内容，总计40个字符。本规范中用到的 infohash 字符串是全部大写的。

#### 3.0.3 连接列表的连接状态机制（`活动`、`不活动`、`不存在`）

连接列表是自己维护的，里面的项有两个状态：`活动`（active）和`不活动`（inactive）。例如，`A`向`B`尝试连接，如果连接成功，则`A`和`B`都能确认对方在自己的连接列表中，且处于活动状态；如果对方不在列表中，则将对方加入活动列表并设其状态为`活动`，如果对方已经在列表中就直接将其状态设置为`活动`。如果连接失败，则`A`将`B`设置为`不活动`。处于“不活动”状态下的项目，在发送期间正常发送，但是要计算发送次数。如果发送次数达到阈值`n`（例如`3`），而且一直无法连接，则将其移出连接列表。*（注：该阈值可以计算后选定，以确保在大多数情况下能不丢失虚假的不活动状态，而且也不产生过多通信冗余。）*

移出前，设置状态为`即将移出`（remove pending），然后扫描列表移除该状态的项目。也即，`即将移出`状态是一个过渡状态，不会维持太长时间，在下一次扫描列表时这样状态的项会被移除。

`不存在`（not exist）状态就是指不存在于连接列表中。

#### 3.0.4 消息的除重机制

广播消息容易造成消息重复处理，甚至引起消息震荡。

在本规范中，每个消息必须带有源客户端的终端信息，以及一个消息ID，用来区分各条消息。

消息ID是一个64位无符号整数。生成方法无具体规定，但是必须具有（消息生存周期内）的，对于每个客户端来说的唯一性。

在收到广播消息的时候，客户端判断该消息是否处于“已处理消息”列表中。若处于，则不再处理，并将消息已生存时间设为0，重新开始计算生存周期。若不处于，则处理该消息并将其放入“已处理消息”列表，并设定初始生存周期为`lifelength`（例如`3分钟`）。同时，定时删除超出生存周期的消息。*（注：*`lifelength`*值需要仔细选择，不能太长也不能太短，要确保列表长度不太长的条件下，正确过滤已处理消息。)*

#### 3.0.5 客户端的自我认知机制

在版本 1.1 之前，客户端及其附带的 tracker 服务器的自我认知端点是通过用户选择决定的。这种方案有几个缺陷：

1. 缺少经验的用户难以选择合适的地址；
2. 如果通过程序帮助选择，也难以排除一些网卡报告的假地址（如 192.168.100.1，但该地址不可用）；
3. 如果通过路由器上网或者启用了 NAT 穿越，在两端所认知的端点（主要是端口）是不同的，如果一个直接连接到内网的客户端`A`直接向另一个通过路由器连接到内网的客户端`B`报告的自身端点发送信息，则该信息无法到达。

为了解决这个问题，应该让一个直接连接到内网的客户端（即接入点`I`）来承担识别任务。整个分布网络中，所有公开的接入点都必须直接连接到内网。同时，所有的 BitTorrent 客户端需要打开 UPnP 端口映射和 NAT-PMP 端口映射。

自我认知分为两个部分：

- 接入点。接入点必须确保直接连接到内网，其自我认知端点是由有经验的用户手工选择的。
- 普通客户端。自我认知的端点选择方式见下文。

普通客户端`A`在加入分布网络之前，应该设置其自我认知端点地址为环回地址（一般使用`127.0.0.1`），端口为用户设置的端口。此时各个端点设置如下：

- 客户端。地址为环回地址，端口为用户设置的客户端端口。
- Tracker 服务器。地址随意（不使用），端口为用户设置的 tracker 服务器端口。
- BitTorrent 客户端（用于报告用户信息）。地址为环回地址，端口为 BitTorrent 客户端报告的通信端口。

`A`在加入分布网络时，将加入信息发往接入点`I`。`I`在通过 TCP 连接中的对方通信端点获知`A`的端点，并将该信息作为`A`的自我认知端点信息报告，发送给`A`（结构见[5.5节](#part5d5)）。`A`收到后，将收到的自我认知端点分为两个部分，地址`addr`和端口`port`，并进行相应设置。此时各个端点设置如下：

- 客户端。地址为`addr`，端口`port`。
- Tracker 服务器。地址随意（不使用），端口为用户设置的 tracker 服务器端口。
- BitTorrent 客户端（用于报告用户信息）。地址为`addr`，端口为 BitTorrent 客户端报告的通信端口。

> 当前通过 UPnP（手动启动）实现内外网的端口映射，流程如下。（数字内容仅为示例）
>
> 1. 使用端口号为`9029`的端口向直接连接到内网的接入点发送连接请求，本地路由器视角端点`192.168.1.100:9029`，直接接入内网时地址`192.168.2.41`；
> 2. 接入点收到请求，解析来源端点（如`192.168.2.39:1104`，即，地址和端口与原客户端直接接入内网时都可能不一样），并将该端点报告给请求连接的客户端（结构见[5.5节](#part5d5)）；
> 3. 请求的客户端收到端点，发送 UPnP 请求设置由路由器内网端口`9029`到路由器外网端口`1104`的端口映射；
> 4. 在余下的时间，只要网络环境不变化，接入点就认为它在与`192.168.2.39:1104`通信，发起连接的客户端认为自己在使用`9029`端口，而这个通信是可以正常进行的。
>
> *潜在的问题：注意，地址经过映射后可能会改变。也就是说，如果映射后，如果一台位于`192.168.2.39`的客户端使用端口`1104`直接通过内网连接到同一个接入点，则会引发异常。*

### 3.1 Tracker 服务器

> 本规范中并未规定如何建立基本 tracker 服务器，只要求建立的 tracker 服务器能与客户端通信即可。

### 3.2 客户端的连接、断开与状态持续

本节分为4个部分，分别是“加入”、“保持”、“退出”和“掉线”。

#### 3.2.1 加入分布网络

> 客户端连接上分布网络的时候发生。

当某个客户端第一次加入分布网络时，需要通过某个接入点接入。设在该接入点上的客户端为`A`，即将加入的客户端为`C`。

流程如下。

1. `C`向`A`发起连接请求，`A`接受，`C`将需要报告的信息（见[4.3.2节](#part4d3d2)）报告给`A`。
2. `A`将需要报告的信息（见[5.5节](#part5d5)）通过消息返回给`C`，`C`将其加入自己的连接列表，并将`A`也加入连接列表。然后，`A`将`C`加入连接列表。
3. `C`尝试向连接列表中的所有主机广播消息，告知自己加入该网络。
4. 其他客户端（设为`X`）收到该消息。每个客户端独立判断，如果已经处理过该消息，则不再处理；否则，向`C`报告自己存活，给连接列表里的所有客户端转发该消息，然后将`C`加入自己的连接列表。
5. `C`收到回复，将`X`加入自己的连接列表。

#### 3.2.2 状态保持

> 和**lansure**的规范中的心跳机制不同，本规范并不规定如何确认客户端之间保持连接。
>
> 本规范采用`活动`、`不活动`、`不存在`状态机制来管理连接。

#### 3.2.3 退出分布网络

> 客户端申请退出分布网络的时候发生。

设即将退出的客户端为`C`。

流程如下。

1. `C`窗口收到关闭通知，向所有连接列表中的客户端发送关闭消息。
2. `C`的连接列表中的某个客户端（设为`B`）收到该消息，将`C`直接移出连接列表。然后，`B`也要将处于`B`本地的关联到`C`的用户列表删除。

#### 3.2.4 意外掉线

> 客户端退出时候未正常发送退出消息时发生。
>
>> 常见于用户直接关闭计算机时。部分未来花园用户养成了关机时不关闭 μTorrent 而直接按下关机按钮的习惯，因此必须假设他们对客户端会做出同样的事情。
>
> 本规范中并未规定对这种情况的处理，请求将正常发送。本规范采用`活动`、`不活动`、`不存在`状态机制来管理连接。

### 3.3 用户连接信息通告

本节分为2个部分，分别是“加入”与“退出”。

#### 3.3.1 用户加入

> Tracker 服务器收到 μTorrent 发来的，参数`infohash`为指定 infohash，参数`state`的值为`start`的请求时发生。
>
>> 一般发生在添加种子，或者按下“开始任务”按钮时。

用户加入过程与客户端加入分布网络时类似。不过此处发出的消息为用户加入，而且参数需要带上用户与 infohash 信息。

收到消息后，收到消息的客户端将消息中报告的用户加入指定 infohash 所对应的列表中。如果该列表不存在则创建之，再加入条目。

如果主机的 μTorrent 重启，在此期间客户端持续运行，则会出现重复添加本地用户的情况。因此，在添加时，应该先检查列表中是否包含即将要添加的用户。

此外，应该保证消息的双向传递。在广播时，消息只能从广播源开始向下游发送，但是这就无法让广播源了解有哪些具有同样的 infohash 的用户存在。因此，收到用户进入消息时，检查自己的种子列表，如果存在具有这个 infohash 的种子的用户列表，那就应该向广播源发送一条消息（`GotPeer`）让广播源添加本机。

*注意：tracker 服务器此时应该__异步__触发事件。*

#### 3.3.2 用户退出

> Tracker 服务器收到 μTorrent 发来的，参数`infohash`为指定 infohash，参数`state`的值为`stop`（或`pause`）的请求时发生。
>
>> 一般发生在删除种子，或者按下“停止任务”按钮时。

用户退出过程与客户端退出分布网络时类似。不过此处发出的消息为用户加入，而且参数需要带上用户与 infohash 信息。

收到消息后，收到消息的客户端将消息中报告的用户从指定 infohash 所对应的列表中移除。如果移除后列表为空，则将列表删除。

*注意：tracker 服务器此时应该__异步__触发事件。*

### 3.4 查找具有指定 infohash 值的种子

> Tracker 服务器收到 μTorrent 发来的，参数`infohash`为指定 infohash 的请求时发生。
>
>> μTorrent 会定期向 tracker 服务器查询用户连接信息，因此这个事件会定期发生。

实际上，这是 μTorrent 向 tracker 服务器发起的请求。

由于用户加入和退出的时候都是进行传递式广播的，这样更改只会发生在收到用户加入/退出的消息时。查询可以直接查询本地数据库。

不管什么情况，都应该立即返回一个列表。当查询得到的列表为空（首次添加该任务）时，应该加入主机上的用户，再向其他客户端广播“用户加入”消息。

*注意：tracker 服务器此时应该__同步__触发事件。*

### 3.5 查询分布网络状态

> 本规范暂时不提供查询分布网络状态的功能。
>
> **lansure**的规范中，可以通过模拟函数调用过程，在进行广播时通过返回计数的方法获得网络中节点的个数。

### 3.6 数据备份

> 考虑到用户增多时完整网络数据（包含客户端列表和用户列表等）可能会较大，在不进行本地缓存的情况下对部分主机造成负担，因此本规范暂时不提供客户端之间备份完整网络数据的功能。
>
> **lansure**的规范中，每个节点都保存着该网络中的所有数据，数据更改只在收到广播时进行，因此可以认为所有节点是几乎完全一样的，就是说其他所有节点都是本节点的备份。

<span style="color:red; font-weight:bold;">具体计算未进行，请稍后补充。</span>

## IV 通信协议结构

### 4.1 头结构定义

```
struct KMessage
{
  uint64 MagicNumber;           // = 0x00756968 0x61727500 = "uiharu"
  KMessageHeader Header;
  KMessageContent Content;
}
```

其中头相关定义如下。

```
struct KMessageHeader
{
  uint32 HeaderLength;
  uint16 HeaderVersion;
  uint64 MessageID;
  KMessageCode Code;
  KEndPoint SourceEndPoint;
}
```
```
struct KEndPoint
{
  uint8[4] Address;
  uint8[2] Port;
}
```

内容相关定义如下。

```
struct KMessageContent
{
  uint32 DataLength;
  uint8[] Data;
}
```

### 4.2 消息代码（message code）定义

```
enum KMessageCode : int32
{
  EmptyMessage,                 // 0
  ReportAlive,                  // 1
  ClientEnterNetwork,           // 2
  ClientExitNetwork,            // 3
  PeerEnterNetwork,             // 4
  PeerExitNetwork,              // 5
  GotPeer,                      // 6
}
```

### 4.3 对应消息代码的数据内容定义

以下的`struct Content`，是为了可读性而描述的。实际使用的时候，应该转换为字节数组，存放在`KMessageContent`的`Data`字段，并正确设置`KMessageContent`的`DataLength`字段。

#### 4.3.1 `ReportAlive`

无内容。

#### <span id="part4d3d2">4.3.2 `ClientEnterNetwork`</span>

```
struct Content
{
  int32 IsHandled;      // 其他客户端是否已经处理过这个消息。
                        // 为零表示未处理过（本客户端是接入点），
                        // 应该返回自己的连接列表和用户列表；
                        // 非零表示只需要转发。
                        // 如果是第一次处理，那么要将这个值设为非零值，再广播。
  uint16 RealPort;		// 发起请求的客户端的真实监听端口。
						// 为零表示发起请求的是一个普通用户，按照默认方式
						// 添加端点；非零表示发起请求的是一个接入点，
						// RealPort 是其监听端口。
}
```
```
d:
{
  "message handled": 1/0	// 其他客户端是否已经处理过这个消息。
							// 为零表示未处理过（本客户端是接入点），
							// 应该返回自己的连接列表和用户列表；
							// 非零表示只需要转发。
							// 如果是第一次处理，那么要将这个值设为非零值，再广播。
  "real port": port(u16)	// 发起请求的客户端的真实监听端口。
							// 为零表示发起请求的是一个普通用户，按照默认方式
							// 添加端点；非零表示发起请求的是一个接入点，
							// RealPort 是其监听端口。
}
```

#### 4.3.3 `ClientExitNetwork`

无内容。

#### 4.3.4 `PeerEnterNetwork`

```
struct Content
{
  uint8[20] InfoHash;          // 20字节的 infohash
  uint16 BitTorrentClientPort;   // 本地 BitTorrent 客户端的连接端口号
}
```
```
d:
{
  "infohash": infohash(20)
  "bt client port": port(u16)
}
```

#### 4.3.5 `PeerExitNetwork`

```
struct Content
{
  uint8[20] InfoHash;          // 20字节的 infohash
  uint16 BitTorrentClientPort;   // 本地 BitTorrent 客户端的连接端口号
}
```
```
d:
{
  "infohash": infohash(20)
  "bt client port": port(u16)
}
```

#### 4.3.6 `GotPeer`

```
struct Content
{
  uint8[20] InfoHash;          // 20字节的 infohash
  uint16 BitTorrentClientPort;   // 本地 BitTorrent 客户端的连接端口号
}
```
```
d:
{
  "infohash": infohash(20)
  "bt client port": port(u16)
}
```

## V 程序用结构

### 5.1 客户端

```
class ConnectionListItem
{
  KClientLocation ClientLocation;
  ConnectionState State;
  uint32 TimesTried;
}
```

连接状态是一个枚举，结构如下。

```
enum ConnectionState
{
  Active,           // 活动状态
  Inactive,         // 不活动状态
  RemovePending,    // 即将移出状态
}
```
```
struct KClientLocation
{
  KEndPoint EndPoint;
}
```

### 5.2 用户

```
struct Peer
{
  KEndPoint EndPoint;
  int32 BitTorrentClientPort;
}
```

### 5.3 种子集合

网络内的所有种子是一个字典，键为对应的 infohash 字符串，值为对应的用户列表，结构如下。

```
Dictionary<string, List<Peer>> AllTorrents;
```

### 5.4 “已处理消息”列表

“已处理消息”列表是一个线性表，按照加入时间排序，应具有一定的初始长度以避免过多的动态内存分配。其结构如下。

```
List<KHandledMessage> HandledMessages;
```

列表项项目如下。

```
class KHandledMessage
{
  KMessage Message;
  DateTime LifeStart;
  TimeSpan LifeLength;
}
```

### 5.5 <span id="part5d5">接入点告知接入客户端的信息</span>

结构如下，经过 B-编码处理，需要编码和解码。

```
d
{
  "connections": l
  {
    d
    {
      "ip": ip(4)
      "port": port(2)
    }
    ...
  }
  "peers": d
  {
    infohash_1: l
    {
      d
      {
        "ip": ip(4)
        "port": port(2)
      }
      ...
    }
    ...
  }
  "your endpoint": d
  {
    "ip": ip(4)
    "port": port(2)
  }
}
```

------

## 附录

### I 更新记录

#### 版本 1.0

- 初始版本（已经丢失）。

#### 版本 1.1

- 对连接方式作出修正。

#### 版本 1.2

- 取消普通客户端的端点自识别，而加入客户端的自我认知机制（以用于兼容路由器上网及 NAT）。