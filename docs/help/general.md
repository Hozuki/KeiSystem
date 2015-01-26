# KeiSystem 简介

KeiSystem 是一个分布式内网 tracker 网络构建软件。它通过在每个用户的机器上搭建小型 tracker 服务器并将它们连接起来，构成一个大型的文件分享网络。在这个网络内，可以几乎和 BT 站一样地进行文件分享。

KeiSystem 基于 .NET Framework 4（在 MacOS/Linux 中对应的是 Mono 2.8.0+）。如果您不确定自己的计算机上是否已安装，可以到这里来下载：[Windows](http://www.microsoft.com/zh-cn/download/details.aspx?id=17718)、[MacOS/Linux](http://www.mono-project.com/download/)。

## <span id="faq">FAQ</span>

> **Q: 什么是 tracker？**<br/>**A:** Tracker 是让 BitTorrent 客户端（例如 μTorrent、BitComet、Transmission）能知道下载某个种子的部分或所有用户情况的协议。BT 客户端和 tracker 服务器通过 tracker 协议沟通，而 tracker 服务器存放着每一个 BT 客户端的位置信息，并告知需要知道该信息的 BT 客户端。通过解读每次与 tracker 通信的返回数据，BT 客户端就可以知道如何连接到同时拥有某个种子的其他 BT 客户端，并进行文件传输。使用过未来花园的同学，是否还记得个人设置页面中有个人的“tracker 地址”呢？花园后台就运行着一个 tracker 服务器，每个人的 BT 客户端就是和它进行通信，同学们才能看到出现上传和下载过程。
> 
> **Q: KeiSystem 的优势在什么地方？**<br/>**A:** 1、为了照顾花园的老用户，我最大化地保留的花园的操作手感，将来花园完全恢复的时候可以尽快切换到花园的操作。*我相信花园还会再开，这就是我编写 KeiSystem 的动力。*<br/>2、由于通过 KeiSystem 获得的只是用户网络位置，而且软件内限定为校内使用，传输通过 BT 客户端完成，因此不会产生外网流量，也就不用占用外网出口带宽。<br/>3、每个 tracker 服务器只需要处理本机的 tracker 请求，和与其他 KS 客户端的通信，将处理的压力分担到了 KS 网络中的每台计算机上，所以对单台计算机的造成的压力不大。
>
> **Q: 使用的时候要注意什么？**<br/>**A:** 1、KeiSystem 的特点是，同时运行的人越多，里面的种子越多，力量越大。所以，希望您能尽量持续运行 KeiSystem 的客户端，让种子在 KS 网络中生存，大家才能下载呀！<br/>2、由于条件限制，分享的信息毕竟不会像花园还健全时发布的内容那么清晰直观，希望大家到论坛的专用部分发布种子，同时**制作种子时请按照[教程](./tnorm.htm)制作**。<br/>3、最重要的，即使没有了统计的上传下载，也请大家积极参与，我为人人，人人为我，谢谢……
>
> **Q: KeiSystem 是怎么运行起来的？**<br/>**A:** 每个人启动 KeiSystem 客户端后，该客户端会启动用于本机的小型 tracker 服务器，同时负责与其他 KS 客户端通信，更新状态。在种子信息中填入本机的 tracker 服务器地址，BT 客户端就会尝试与本机的 tracker 服务器通信。同时，本机的 tracker 服务器查找连接到 KS 网络内的 KS 客户端所持有的种子，若找到了就会报告给 BT 客户端。简单说就是这样的啦☆~如果你是专业人士，欢迎查看本项目的[主页](https://github.com/GridScience/KeiSystem/)，从代码中知道更详细的信息。
>
> **Q: KeiSystem 这个名字是怎么来的？**<br/>**A:** 你猜……

------

最后更新于 2015-01-27

[返回主页](./index.htm)

Copyright (C) 2015 micstu@FGBT