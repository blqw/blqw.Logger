if ("$blqw" in window == false) {
    $blqw = {};
    kk = $blqw;
}
//配置
$blqw.config = {};
//数据
$blqw.data = {};

$blqw.error = function (message) {
    $blqw.popup(message, "error");
};

//触发事件
$blqw.fireEvent = function (jq, event) {
    $(jq).each(function () {
        if (this.isFireEvent) {
            return;
        }
        this.isFireEvent = true;
        $(this).trigger(event);
        delete this.isFireEvent;
    });
};

//获取参数
$blqw.getElementArgs = function (jq, option) {
    try {
        var args = $(jq).eq(0).attr("-data");
        if (args) {
            args = eval("(" + args + ")");
        }
        if ($.isPlainObject(args)) {
            return args;
        }
        if (option) {
            args = args || {};
            for (var i in option) {
                if (i in args == false) {
                    agrs[i] = option[i];
                }
            }
        }
        return args || {};
    } catch (e) {
        return option || {};
    }
};

//克隆对象
$blqw.clonePlainObject = function (obj) {
    return $.extend({}, obj);
};

//合并对象,数值以obj1为主
$blqw.mergePlainObject = function (obj1, obj2) {
    var args = $.merge([true, {}], arguments)
    return $.extend.apply($, args);
}

//分析方法参数
$blqw.parseArgs = function () {
    var args = {
        Number: [],
        String: [],
        Boolean: [],
        Array: [],
        Json: [],
        DateTime: [],
        Object: [],
        Function: [],
        JQuery: [],
        Dom: []
    };
    for (var i = 0; i < arguments.length; i++) {
        var v = arguments[i];
        if (v !== null && v !== undefined) {
            if (v.constructor === Number) {
                args.Number.push(v);
            } else if (v.constructor === String) {
                args.String.push(v);
            } else if (v.constructor === Boolean) {
                args.Boolean.push(v);
            } else if (v.constructor === Date) {
                args.DateTime.push(v);
            } else if (v.constructor === Array) {
                args.Array.push(v);
            } else if (v.constructor === Function) {
                args.Function.push(v);
            } else if (v instanceof jQuery) {
                args.JQuery.push(v);
            } else if ("HTMLElement" in window ? v instanceof HTMLElement : v.nodeType === 1) {
                args.Dom.push(v);
            } else if ($.isPlainObject(v)) {
                args.Json.push(v);
            }
        }
    }
    return args;
};

//动态加载文件
$blqw.include = function (file) {
    var option = $blqw.parseArgs.apply(window, arguments);
    var files = option.Array[0] || option.String;
    var callback;
    var length = files.length;
    var ok = 0;
    if (option.Function.length > 0) {
        callback = function () {
            ok++;
            if (ok == length) {
                option.Function[0]();
            }
        };
    }

    for (var i = 0; i < length; i++) {
        var name = $.trim(files[i]);
        var att = name.split('.');
        var ext = att[att.length - 1].toLowerCase();
        if (ext == "css") {
            $("<link />").attr({ type: "text/css", rel: "stylesheet", "href": name }).appendTo("head");
            ok++;
        } else {
            $.getScript(name, callback);
        }
    }
};

//填充table数据
$blqw.fillTable = function (selector, data, setData) {
    var table = $(selector)
    if (table.length != 1 || table.is("table") == false) {
        throw Error("绑定对象太多或不是table");
    }

    var func = null;
    if (setData && setData.constructor === Function) {
        func = setData;
    }

    var template = table.data("grid-template");
    if (!template) {
        template = table.find("tr[template]");
        if (template.length == 0) {
            template = table.find("tbody tr:first");
        }
        table.data("grid-template", $("<div />").append(template.attr("data-row", "").show()).html());
        template.remove();
        template = table.data("grid-template");
    } else {
        table.find("[data-row]").remove();
    }
    if (!data || data.constructor !== Array) {
        return;
    }

    var tbody = table.find("tbody");
    for (var i in data) {
        var it = data[i];
        var html = template.replace(/\{[^{}]+\}/g, function (a) {
            var name = a.slice(1, -1);
            if (func) {
                var value = func(it, name);
                if (value !== undefined) {
                    return $blqw.IsNull(value);
                }
            }
            return $blqw.encodeHTML(it[name]);
        });
        tbody.append(html);
    }
};

//填充除table以外的循环数据
$blqw.fillList = function (selector, data, setData) {
    var list = $(selector)
    if (list.length != 1) {
        throw Error("绑定对象太多");
    }

    var func = null;
    if (setData && setData.constructor === Function) {
        func = setData;
    }

    var template = list.data("grid-template");
    if (!template) {
        template = list.find("[template]");
        if (template.length == 0) {
            template = list.find("> *");
        }
        list.data("grid-template", $("<div />").append(template.attr("data-item", "").show()).html());
        template.remove();
        template = list.data("grid-template");
    } else {
        list.find("[data-item]").remove();
    }
    if (!data || data.constructor !== Array) {
        return;
    }

    for (var i in data) {
        var it = data[i];
        var html = template.replace(/\{[^{}]+\}/g, function (a) {
            var name = a.slice(1, -1);
            if (func) {
                var value = func(it, name);
                if (value !== undefined) {
                    return $blqw.IsNull(value);
                }
            }
            return $blqw.encodeHTML(it[name]);
        });
        list.append(html);
    }
};

//填充不循环的数据
$blqw.fillData = function (selector, data, setData) {
    var panel = $(selector)
    if (panel.length == 1 && panel.is("table")) {
        $blqw.fillTable(selector, data, setData);
        return;
    } else if (panel.length == 0) {
        return;
    } else if (panel.length > 1) {
        panel.each(function () {
            $blqw.fillData(this, data, setData);
        });
        return;
    }
    var func = null;
    if (setData && setData.constructor === Function) {
        func = setData;
    }
    var template = panel.data("grid-template");
    if (!template) {
        template = panel.html();
        panel.data("grid-template", template);
    } else {
        panel.html('');
    }
    if (data) {
        var html = template.replace(/\{[^{}]+\}/g, function (a) {
            var name = a.slice(1, -1);
            if (func) {
                var value = func(data, name);
                if (value !== undefined) {
                    return $blqw.IsNull(value);
                }
            }
            return $blqw.encodeHTML(data[name]);
        });
        panel.html(html);
    }
};

//格式化json对象或json字符串
$blqw.jsonFormat = function (json) {
    var obj = json;
    if (json.constructor === String) {
        obj = JSON.parse(json);
    }

    var buffer = [];

    var space = function (c) {
        for (var i = 0; i < c; i++) {
            buffer.push("&nbsp;");
        }
    }

    var append = function (o, d) {
        if ($.isArray(o)) {
            buffer.push("[");
            var b = false;
            for (var i in o) {
                if (b) {
                    buffer.push(" ,");
                }
                buffer.push("<br />");
                b = true;
                space((d + 1) * 2);
                append(o[i], d + 1);
            }
            buffer.push("<br />");
            space(d * 2);
            buffer.push("]");
        } else if ($.isPlainObject(o)) {
            buffer.push("{");
            var b = false;
            for (var i in o) {
                if (b) {
                    buffer.push(" ,");
                }
                buffer.push("<br />");
                b = true;
                space((d + 1) * 2);
                buffer.push(JSON.stringify(i));
                buffer.push(" : ");
                append(o[i], d + 1);
            }
            buffer.push("<br />");
            space(d * 2);
            buffer.push("}");
        } else {
            buffer.push(JSON.stringify(o));
        }
    }
    append(obj, 0);
    return buffer.join("");
};

//获取url参数值
$blqw.queryString = function (name) {
    var rex = new RegExp("[?&]\s*" + name + "\s*=([^&$#]*)", "i");
    var r = rex.exec(location.search);
    if (r && r.length == 2)
        return decodeURIComponent(r[1]);
}

$blqw.IsNull = function (obj, defaultValue) {
    if (obj != null) {
        return obj;
    }
    if (defaultValue == null) {
        return "";
    }
    return defaultValue;
}

$blqw.encodeHTML = function (data) {
    if (data == null) {
        return "";
    }
    return data.toString().replace(/[<>&" \t]/g, function (m) {
        switch (m) {
            case "<":
                return "&lt;";
            case ">":
                return "&gt;";
            case "&":
                return "&amp;";
            case '"':
                return "&quot;";
            case " ":
                return "&nbsp;";
            case "\t":
                return "&nbsp;&nbsp;&nbsp;&nbsp;";
            default:
                break;
        }
    })
}

$blqw._api = function (url) {
    var me = this;
    me.baseUrl = url;

    function processResult(result, success, fail) {
        if ("Code" in result && "Message" in result && "Data" in result) {
            if (result.Code == 1) {
                kk.alert(result.Message);
                return;
            }
            if (result.Code == 3) {
                document.write(result.Message);
                return;
            }
            if (result.Code == 302) {
                kk.loading();
                window.location = result.Message + window.location.hash;
                return;
            }
            if (result.Code == 4002) {
                kk.loading();
                if ($blqw.config.login.url) {
                    $blqw.config.login.target.location = $blqw.urlCombine(me.baseUrl, $blqw.config.login.url);
                } else {
                    kk.popup("用户为登陆", "err");
                }
                return;
            }
            if (result.Code != 0) {
                if (fail) {
                    fail(result);
                } else {
                    kk.popup(result.Message, "err");
                }
                return;
            }
            result = result.Data;
        }
        try {
            success && success(result);
        } catch (e) {
            $blqw.error("回调方法异常:" + e.message);
        }
    };

    function ajax(method, route, data, success, fail) {
        if ($.isFunction(data)) {
            if ($.isFunction(success)) {
                fail = success;
            }
            success = data;
            data = null;
        }
        data = data || {};
        data.agent = "ajax";
        data.uk || (data.uk = $blqw.queryString("uk"));
        data._r_ = Math.random();
        $blqw.loading();
        return $.ajax({
            dataType: "json",
            url: $blqw.urlCombine(me.baseUrl, route),
            type: method,
            data: data,
            success: function (r) {
                processResult(r, success, fail);
            }
        });
    }
    this.get = function (route, data, success, fail) {
        return ajax("GET", route, data, success, fail);
    }
    this.post = function (route, data, success, fail) {
        return ajax("POST", route, data, success, fail);
    }
    this.jsonp = function (route, data, success, fail) {
        if ($.isFunction(data)) {
            if ($.isFunction(success)) {
                fail = success;
            }
            success = data;
            data = null;
        }
        data = data || {};
        data.agent = "jsonp";
        data.uk || (data.uk = $blqw.queryString("uk"));
        kk.loading();

        var result = $.ajax({
            dataType: "jsonp",
            url: $blqw.urlCombine(me.baseUrl, route),
            type: "POST",
            data: data,
            success: function (result) {
                if (result.isTimeout == null) {
                    kk.unLoading();
                    processResult(result, success, fail);
                }
            }
        });
        setTimeout(function () {
            result.isTimeout = true;
            if (result.readyState == 1) {
                if (fail) {
                    fail();
                } else {
                    kk.popup("请求超时:" + $blqw.urlCombine(me.baseUrl, route), "err");
                }
                $blqw.unLoading();
            }
        }, $blqw.config.jsonpTimeout);
    }
};

(function () {
    $blqw.api = new $blqw._api(window.location.origin || (window.location.protocol + "//" + window.location.host));
})();

$blqw.api.add = function (name, url) {
    this[name] = new $blqw._api(url);
};


//处理url
$blqw.urlCombine = function (url, route) {
    if (route == null) {
        return url;
    }
    if (route.indexOf('://') > 0) {
        return route;
    }
    if (route.charAt(0) == '/') {
        var start = url.indexOf("://") + 3;
        var length = url.substring(start).indexOf("/");
        if (length > 0) {
            var domain = url.substring(0, start + length);
            return domain + route;
        }
        return url + route;
    }
    if (url.slice(-1) != '/') {
        return url + "/" + route;
    }
    return url + route;
}

//其他功能
//格式化时间
var ___DateToString = Date.prototype.toString;
Date.prototype.toString = function (format) {
    if (format === undefined) {
        return ___DateToString.apply(this);
    }
    if (format === null) {
        format = "yyyy-MM-dd HH:mm:ss";
    }

    format = format.replace(/yyyy/g, this.getFullYear());
    format = format.replace(/yyy/g, this.getYear());
    format = format.replace(/yy/g, this.getFullYear().toString().slice(-2));

    if (format.indexOf('mi') >= 0) {
        format = format.replace(/mi/g, this.getMilliseconds().toString());
    }

    if (format.indexOf('M') >= 0) {
        var M = (this.getMonth() + 1).toString();
        format = format.replace(/MM/g, ("0" + M).slice(-2));
        format = format.replace(/M/g, M);
    }

    if (format.indexOf('ddd') >= 0) {
        var xq = ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];
        format = format.replace(/ddd/g, xq[this.getDay()]);
    }

    if (format.indexOf('d') >= 0) {
        var d = this.getDate().toString();
        format = format.replace(/dd/g, ("0" + d).slice(-2));
        format = format.replace(/d/g, d);
    }


    if (format.indexOf('h') >= 0 || format.indexOf('H') >= 0) {
        var h = this.getHours().toString();
        format = format.replace(/HH/g, ("0" + h).slice(-2));
        format = format.replace(/H/g, h);
        h = h % 12;
        format = format.replace(/hh/g, ("0" + h).slice(-2));
        format = format.replace(/h/g, h);
    }

    if (format.indexOf('m') >= 0) {
        var m = this.getMinutes().toString();
        format = format.replace(/mm/g, ("0" + m).slice(-2));
        format = format.replace(/m/g, m);
    }

    if (format.indexOf('s') >= 0) {
        var s = this.getSeconds().toString();
        format = format.replace(/ss/g, ("0" + s).slice(-2));
        format = format.replace(/s/g, m);
    }

    if (format.indexOf('f') >= 0) {
        var f = this.getMilliseconds().toString();
        format = format.replace(/fff/g, ("000" + f).slice(-3));
        format = format.replace(/ff/g, ("000" + f).slice(-3).substr(0, 2));
        format = format.replace(/f/g, ("000" + f).slice(-3).substr(0, 1));
    }
    //.getMilliseconds()

    return format;
};
Date.prototype.checkNumber = function (value) {
    if (+value !== value) {
        throw new Error("必须是数字");
    }
};

//时间加减
Date.prototype.add = function (type, value) {
    this.checkNumber(value);
    var date = new Date(this);
    switch (type) {
        case 'y':
        case 'Y':
            date.setYear(this.getYear() + value);
            break;
        case 'M':
            date.setMonths(this.getMonths() + value);
            break;
        case 'd':
        case 'D':
            date.setDate(this.getDate() + value);
            break;
        case 'h':
        case 'H':
            date.setHours(this.getHours() + value);
            break;
        case 'm':
            date.setMinutes(this.getMinutes() + value);
            break;
        case 'S':
        case 's':
            date.setSeconds(this.getSeconds() + value);
            break;
        case 'f':
            date.setMilliseconds(this.getMilliseconds() + value);
            break;
        default:
            throw Error("type只能是:y,Y,M,d,D,h,H,m,s,S,f");
    }
    return date;
};

Date.prototype.addDays = function (value) { return this.add("d", value); };
Date.prototype.addHours = function (value) { return this.add("h", value); };
Date.prototype.addMilliseconds = function (value) { return this.add("f", value); };
Date.prototype.addMinutes = function (value) { return this.add("m", value); };
Date.prototype.addMonths = function (value) { return this.add("M", value); };
Date.prototype.addSeconds = function (value) { return this.add("s", value); };
Date.prototype.addYears = function (value) { return this.add("y", value); };


//是否处于loading状态
$blqw.isLoading = false;

//显示loading框
$blqw.loading = function () {
    var id = "$blqw-loading";
    if (this.isLoading) {
        return false;
    }
    $blqw.isLoading = true;
    $("body").append("<div shade='shade' style='position:absolute;width:100%;height:100%;top:0;left:0;' />").css("position", "relative"); //loding前遮罩
    var config = { id: id, height: "40px", cancel: function () { return false; }, cancelDisplay: false };
    var dialog = this.dialog;
    setTimeout(function () { dialog(undefined, true, config); }, this.config.loadingDelay);
    return true;
};

//关闭loading框
$blqw.unLoading = function () {
    $("body > div[shade='shade']").remove();
    var id = "$blqw-loading";
    if (this.isLoading) {
        $blqw.isLoading = false;
        if ("dialog" in window) {
            var x = dialog.get(id);
            x && x.remove();
        }
        return true;
    }
    return false;
}

//ajax全局处理
jQuery(document).ajaxStart(function () {
    $blqw.loading();
});
jQuery(document).ajaxStop(function () {
    $blqw.unLoading();
});
jQuery(document).ajaxError(function (event, result, ajax) {
    if (result.responseText && result.responseText.substr(0, 12) == "error_debug:") {
        $blqw.confirm(result.responseText, "DEBUG");
    } else {
        $blqw.popup("请求服务器: " + ajax.url + " 失败\n原因: " + result.statusText, "err");
    }
});

$(window).on("beforeunload", function () {
    if (navigator || (/(iPhone|iPad|iPod)/i).test(navigator.userAgent) == false) { //判断safari浏览器

    }
});

//拓展jquery方法
jQuery.prototype.attrop = function (name, value) {
    if (value) {
        this.attr(name, value);
        this.prop(name, value);
        return this;
    } else {
        return this.attr(name) || this.prop(name);
    }
}
//处理页面所有a标签
$("a:not([href]),a[href=#]").attrop("href", "javascript:void(0)");