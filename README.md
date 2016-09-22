# blqw.Logger
基于微软 System.Diagnostics.TraceListener 封装

```xml
<system.diagnostics>
  <sources>
    <source name="blqw.Logger" switchValue="Warning"> <!-- 修改组件本身异常记录的跟踪,可以忽略, switchValue默认为Error -->
      <listeners> <!-- 默认组件自身异常记录到本地文件,此处修改默认行为 -->
        <clear />
        <add name="blqw.Logger" type="SystemLogTraceListener, blqw.Logger" />  <!-- 写入系统事件,需要管理员权限 -->
      </listeners>
    </source>
  </sources>
  <trace autoflush="false" useGlobalLock="false">
    <listeners>
      <clear /> <!-- 清除默认侦听器 -->
      <add name"logger1" type="SLSTraceListener, blqw.Logger" initializeData="d:\sls2_logs" queueMaxLength="50000000" level="Error" />
      <!--
          type : 侦听器类型,固定为SLSTraceListener, blqw.Logger(必须)
          initializeData : 日志文件记录位置(必须)
          queueMaxLength : 最大缓存队列长度(选填,默认50000000)
          level : 跟踪日志等级(选填,默认为All,可选值参考System.Diagnostics.SourceLevels属性)
       -->
    </listeners>
  </trace>
</system.diagnostics>
```

## 性能
本地测试500线程 每个线程循环1000次 每次写入100条日 共5000万条日志  
测试机配置: i7 6700k 内存ddr4 3200 32G 硬盘 希捷 1T w:100,r:150 ,缓存队列 5000万  
压入日志耗时:73s 全部写完耗时:250s  
压入日志CPU:50\~60% 写日志CPU:20\~30%  
内存峰值:2700Mb 日志量:2.64G 文件数:539 硬盘写入:11mb/秒  
写入日志:20万条/s

## logstash2.2.2
cvs.conf
``` 
input {
    file {
        type => "csv_log_1"
        path => ["根据web.config中日志的输出位置填写/*/*/*.log"]
        start_position => "beginning"
    }
}
filter {
    if [type] == "csv_log_1" {
        csv {
            separator => ","
            columns => ["time", "uid", "level", "topic", "content", "search"]
        } 
    }
}
output {
    if [type] == "csv_log_1" {
        logservice {
            codec => "json"
            endpoint => "cn-hangzhou-vpc.log.aliyuncs.com" //可能会有变化
            project => "webapi-log2" //可能会有变化
            logstore => "cvs_log" //可能会有变化
            topic => ""
            source => ""
            access_key_id => "根据实际情况填写"
            access_key_secret => "根据实际情况填写"
            max_send_retry => 10
        }
    }
}
```
## 更新日志
### [v1.2.7] 2016.09.22
* 修复bug
* 正式版

### [v1.2.6] 2016.09.21
* 优化代码
* 修复SLS异步刷新异常不会记录的问题

### [v1.2.5] 2016.09.20
* 优化代码

### [v1.2.4] 2016.09.19
* 修复SLS日志索引丢失的问题

### [v1.2.3] 2016.09.19
* 优化系统事件日志的显示

### [v1.2.2] 2016.09.19
* 修复sls日志输出等级为字符串的问题(应该为数字)

### [v1.2] 2016.09.19
* 增加输出到系统事件的日志侦听器

### [v1.1] 2016.09.18
* 优化日志组件异常时的本地log输出
* 优化日志输出到文件的方式
* 优化代码,完善注释

### [v1.0] 2016.09.13
* 初始版本