var WebSocketLibrary =
{
	$webSocketManager:
	{
		/*
		 * Map of instances
		 *
		 * Instance structure:
		 * {
		 * 	url: string,
		 * 	ws: WebSocket
		 * }
		 */
		instances: {},

		/* Last instance ID */
		lastId: 0,

		/* Event listeners */
		onOpen: null,
		onMesssage: null,
		onError: null,
		onClose: null,

		/* Debug mode */
		debug: false
	},

	/**
	 * Set onOpen callback
	 *
	 * @param callback Reference to C# static function
	 */
	WebSocketSetOnOpen: function (callback) {
		webSocketManager.onOpen = callback;
	},

	/**
	 * Set onMessage callback
	 *
	 * @param callback Reference to C# static function
	 */
	WebSocketSetOnMessage: function (callback) {
		webSocketManager.onMessage = callback;
	},

	/**
	 * Set onMessage callback
	 *
	 * @param callback Reference to C# static function
	 */
	WebSocketSetOnMessageStr: function (callback) {
		webSocketManager.onMessageStr = callback;
	},

	/**
	 * Set onError callback
	 *
	 * @param callback Reference to C# static function
	 */
	WebSocketSetOnError: function (callback) {
		webSocketManager.onError = callback;
	},

	/**
	 * Set onClose callback
	 *
	 * @param callback Reference to C# static function
	 */
	WebSocketSetOnClose: function (callback) {
		webSocketManager.onClose = callback;
	},

	/**
	 * Allocate new WebSocket instance struct
	 *
	 * @param url Server URL
	 */
	WebSocketAllocate: function (url) {
		var urlStr = Pointer_stringify(url);
		var id = webSocketManager.lastId++;
		webSocketManager.instances[id] = {
			url: urlStr,
			ws: null
		};
		return id;
	},

	/**
	 * Remove reference to WebSocket instance
	 *
	 * If socket is not closed function will close it but onClose event will not be emitted because
	 * this function should be invoked by C# WebSocket destructor.
	 *
	 * @param instanceId Instance ID
	 */
	WebSocketFree: function (instanceId) {
		var instance = webSocketManager.instances[instanceId];
		if (!instance) return 0;

		// Close if not closed
		if (instance.ws !== null && instance.ws.readyState < 2)
			instance.ws.close();

		// Remove reference
		delete webSocketManager.instances[instanceId];

		return 0;
	},

	/**
	 * Connect WebSocket to the server
	 *
	 * @param instanceId Instance ID
	 */
	WebSocketConnect: function (instanceId) {
		var instance = webSocketManager.instances[instanceId];
		if (!instance) return -1;

		if (instance.ws !== null)
			return -2;

		instance.ws = new WebSocket(instance.url);

		instance.ws.onopen = function () {
			if (webSocketManager.debug)
				console.log("[JSLIB WebSocket] Connected.");
			if (webSocketManager.onOpen)
				Runtime.dynCall('vi', webSocketManager.onOpen, [instanceId]);
		};

		instance.ws.onmessage = function (ev) {
			if (webSocketManager.debug)
				console.log("[JSLIB WebSocket] Received message: ", ev.data);

			if (webSocketManager.onMessage === null)
				return;

			if (ev.data instanceof ArrayBuffer) {
				var dataBuffer = new Uint8Array(ev.data);
				var buffer = _malloc(dataBuffer.length);
				HEAPU8.set(dataBuffer, buffer);
				try {
					Runtime.dynCall('viii', webSocketManager.onMessage, [instanceId, buffer, dataBuffer.length]);
				}
				finally {
					_free(buffer);
				}
			}
			else if (ev.data instanceof Blob) {
				var reader = new FileReader();
				reader.addEventListener("loadend", function () {
					var dataBuffer = new Uint8Array(reader.result);
					var buffer = _malloc(dataBuffer.length);
					HEAPU8.set(dataBuffer, buffer);
					try {
						Runtime.dynCall('viii', webSocketManager.onMessage, [instanceId, buffer, dataBuffer.length]);
					}
					finally {
						reader = null;
						_free(buffer);
					}
				});
				reader.readAsArrayBuffer(ev.data);
			}
			else if (typeof ev.data == 'string') {
				var length = lengthBytesUTF8(ev.data) + 1;
				var buffer = _malloc(length);
				stringToUTF8(ev.data, buffer, length);
				try {
					Runtime.dynCall('vii', webSocketManager.onMessageStr, [instanceId, buffer]);
				}
				finally {
					_free(buffer);
				}
			}
			else {
				console.log("[JSLIB WebSocket] not support message type: ", (typeof ev.data));
			}
		};

		instance.ws.onerror = function (ev) {
			if (webSocketManager.debug)
				console.log("[JSLIB WebSocket] Error occured.");

			if (webSocketManager.onError) {
				var msg = "WebSocket error.";
				var length = lengthBytesUTF8(msg) + 1;
				var buffer = _malloc(length);
				stringToUTF8(msg, buffer, length);
				try {
					Runtime.dynCall('vii', webSocketManager.onError, [instanceId, buffer]);
				}
				finally {
					_free(buffer);
				}
			}
		};

		instance.ws.onclose = function (ev) {
			if (webSocketManager.debug)
				console.log("[JSLIB WebSocket] Closed, Code: " + ev.code + ", Reason: " + ev.reason);

			if (webSocketManager.onClose) {
				var msg = ev.reason;
				var length = lengthBytesUTF8(msg) + 1;
				var buffer = _malloc(length);
				stringToUTF8(msg, buffer, length);
				try {
					Runtime.dynCall('viii', webSocketManager.onClose, [instanceId, ev.code, buffer]);
				}
				finally {
					_free(buffer);
				}
			}

			instance.ws = null;
		};
		return 0;
	},

	/**
	 * Close WebSocket connection
	 *
	 * @param instanceId Instance ID
	 * @param code Close status code
	 * @param reasonPtr Pointer to reason string
	 */
	WebSocketClose: function (instanceId, code, reasonPtr) {
		var instance = webSocketManager.instances[instanceId];
		if (!instance) return -1;

		if (instance.ws === null)
			return -3;

		if (instance.ws.readyState === 2)
			return -4;

		if (instance.ws.readyState === 3)
			return -5;

		var reason = (reasonPtr ? Pointer_stringify(reasonPtr) : undefined);

		try {
			instance.ws.close(code, reason);
		}
		catch (err) {
			return -7;
		}

		return 0;
	},

	/**
	 * Send message over WebSocket
	 *
	 * @param instanceId Instance ID
	 * @param bufferPtr Pointer to the message buffer
	 * @param length Length of the message in the buffer
	 */
	WebSocketSend: function (instanceId, bufferPtr, length) {
		var instance = webSocketManager.instances[instanceId];
		if (!instance) return -1;

		if (instance.ws === null)
			return -3;

		if (instance.ws.readyState !== 1)
			return -6;
		console.log(HEAPU8.buffer.slice(bufferPtr, bufferPtr + length));
		instance.ws.send(HEAPU8.buffer.slice(bufferPtr, bufferPtr + length));

		return 0;
	},

	/**
	 * Send message string over WebSocket
	 *
	 * @param instanceId Instance ID
	 * @param stringPtr Pointer to the message string
	 */
	WebSocketSendStr: function (instanceId, stringPtr) {
		var instance = webSocketManager.instances[instanceId];
		if (!instance) return -1;

		if (instance.ws === null)
			return -3;

		if (instance.ws.readyState !== 1)
			return -6;
		//console.log(Pointer_stringify(stringPtr));
		instance.ws.send(Pointer_stringify(stringPtr));

		return 0;
	},

	/**
	 * Return WebSocket readyState
	 *
	 * @param instanceId Instance ID
	 */
	WebSocketGetState: function (instanceId) {
		var instance = webSocketManager.instances[instanceId];
		if (!instance) return -1;

		if (instance.ws)
			return instance.ws.readyState;
		else
			return 3;
	},
	$audioInput: {},
	$recorder: {},
	$chunks: {},
	$audioContext: {},
	Init: function () {

		//初始化录音
		navigator.getUserMedia =
			navigator.getUserMedia ||
			navigator.webkitGetUserMedia ||
			navigator.mozGetUserMedia ||
			navigator.msGetUserMedia

		// 创建音频环境
		try {
			audioContext = new (window.AudioContext || window.webkitAudioContext)()
			audioContext.suspend()
			if (!audioContext) {
				alert('浏览器不支持webAudioApi相关接口')
				return
			}
		} catch (e) {
			if (!audioContext) {
				alert('浏览器不支持webAudioApi相关接口')
				return
			}
		}

		// 获取浏览器录音权限
		if (navigator.mediaDevices && navigator.mediaDevices.getUserMedia) {
			navigator.mediaDevices
				.getUserMedia({
					audio: true,
					video: false,
				})
				.then(function (stream) {
					getMediaSuccess(stream);
				})
				.catch(function (e) {
					getMediaFail(e);
				})
		} else if (navigator.getUserMedia) {
			navigator.getUserMedia(
				{
					audio: true,
					video: false,
				},
				function (stream) {
					getMediaSuccess(stream);
				},
				function (e) {
					getMediaFail(e);
				}
			)
		} else {
			if (navigator.userAgent.toLowerCase().match(/chrome/) && location.origin.indexOf('https://') < 0) {
				alert('chrome下获取浏览器录音功能，因为安全性问题，需要在localhost或127.0.0.1或https下才能获取权限')
			} else {
				alert('无法获取浏览器录音功能，请升级浏览器或使用chrome')
			}
			audioContext && audioContext.close()
			return
		};

		function getMediaSuccess(stream) {
			console.log('getMediaSuccess')

			chunks = [];
			recorder = audioContext.createScriptProcessor(0, 1, 1)
			recorder.onaudioprocess = function (e) {

				chunks.push(new Float32Array(e.inputBuffer.getChannelData(0)))
			}
			// 创建一个新的MediaStreamAudioSourceNode 对象，使来自MediaStream的音频可以被播放和操作
			audioInput = audioContext.createMediaStreamSource(stream)

			audioInput.connect(recorder)
			recorder.connect(audioContext.destination)
		};
		function getMediaFail(e) {
			alert('请求麦克风失败')
			console.log(e)
			audioContext && audioContext.close()
			audioContext = undefined
		};
	},

	StartRecorder: function () {
		//mediaRecorder.start();
		// 连接
		console.log('开始！')
		audioContext.resume()

	},


	StopRecorder: function () {
		//mediaRecorder.stop();

		//停止
		//recorder.disconnect();
		audioContext.suspend()
		console.log('停了！')
		//SendMessage("WebSocket", "OnMessageFloat32ArrayEnd");


		var _data = getFullWavData();//getPureWavData(0);
		sendWAVData(_data, "part");
		_data = [];
		chunks = [];
		//var _headerData = getWAVHeaderData(recordedResultLength);
		//sendWAVData(_headerData, "end");
		//_headerData = [];
		function sendWAVData(blob, sign) {
			var reader = new FileReader();
			reader.onload = function (e) {
				var _value = reader.result;
				var _partLength = 8192;
				var _length = parseInt(_value.length / _partLength);
				if (_length * _partLength < _value.length)
					_length += 1;
				var _head = "Head|" + _length.toString() + "|" + _value.length.toString() + "|" + sign;
				SendMessage("WebSocket", "GetAudioData", _head);
				for (var i = 0; i < _length; i++) {
					var _sendValue = "";
					if (i < _length - 1) {
						_sendValue = _value.substr(i * _partLength, _partLength);
					}
					else {
						_sendValue = _value.substr(i * _partLength, _value.length - i * _partLength);
					}
					_sendValue = "Part|" + i.toString() + "|" + _sendValue;
					SendMessage("WebSocket", "GetAudioData", _sendValue);
				}
				if (sign === "end")
					recorderState = "inactive";
				_value = null;
			}
			reader.readAsDataURL(blob);
		};
		function getFullWavData() {
			var sampleRate = 16000
			var sampleBits = 16
			var bytes = getRawData();
			var dataLength = bytes.length * (sampleBits / 8);
			var buffer = new ArrayBuffer(44 + dataLength);
			var data = new DataView(buffer);
			var offset = 0;
			var writeString = function (str) {
				for (var i = 0; i < str.length; i++) {
					data.setUint8(offset + i, str.charCodeAt(i));
				}
			};
			// 资源交换文件标识符   
			writeString('RIFF'); offset += 4;
			// 下个地址开始到文件尾总字节数,即文件大小-8   
			data.setUint32(offset, 36 + dataLength, true); offset += 4;
			// WAV文件标志  
			writeString('WAVE'); offset += 4;
			// 波形格式标志   
			writeString('fmt '); offset += 4;
			// 过滤字节,一般为 0x10 = 16   
			data.setUint32(offset, 16, true); offset += 4;
			// 格式类别 (PCM形式采样数据)   
			data.setUint16(offset, 1, true); offset += 2;
			// 通道数   
			data.setUint16(offset, 1, true); offset += 2;
			// 采样率,每秒样本数,表示每个通道的播放速度   
			data.setUint32(offset, sampleRate, true); offset += 4;
			// 波形数据传输率 (每秒平均字节数) 单声道×每秒数据位数×每样本数据位/8   
			data.setUint32(offset, 1 * sampleRate * (sampleBits / 8), true); offset += 4;
			// 快数据调整数 采样一次占用字节数 单声道×每样本的数据位数/8   
			data.setUint16(offset, 1 * (sampleBits / 8), true); offset += 2;
			// 每样本数据位数   
			data.setUint16(offset, sampleBits, true); offset += 2;
			// 数据标识符   
			writeString('data'); offset += 4;
			// 采样数据总数,即数据总大小-44   
			data.setUint32(offset, dataLength, true); offset += 4;
			// 写入采样数据   
			data = reshapeWavData(sampleBits, offset, bytes, data);
			//                var wavd = new Int8Array(data.buffer.byteLength);
			//                var pos = 0;
			//                for (var i = 0; i < data.buffer.byteLength; i++, pos++) {
			//                    wavd[i] = data.getInt8(pos);
			//                }                //                return wavd;

			return new Blob([data], { type: 'audio/wav' });

		};
		function getPureWavData(offset) {
			var sampleBits = 16
			var bytes = getRawData();
			var dataLength = bytes.length * (sampleBits / 8);
			var buffer = new ArrayBuffer(dataLength);
			var data = new DataView(buffer);
			data = reshapeWavData(sampleBits, offset, bytes, data);
			//                var wavd = new Int8Array(data.buffer.byteLength);
			//                var pos = 0;
			//                for (var i = 0; i < data.buffer.byteLength; i++, pos++) {
			//                    wavd[i] = data.getInt8(pos);
			//                }                //                return wavd;
			return new Blob([data], { type: 'audio/wav' });
		};
		function reshapeWavData(sampleBits, offset, iBytes, oData) {
			if (sampleBits === 8) {
				for (var i = 0; i < iBytes.length; i++, offset++) {
					var s = Math.max(-1, Math.min(1, iBytes[i]));
					var val = s < 0 ? s * 0x8000 : s * 0x7FFF;
					val = parseInt(255 / (65535 / (val + 32768)));
					oData.setInt8(offset, val, true);
				}
			} else {
				for (var i = 0; i < iBytes.length; i++, offset += 2) {
					var s = Math.max(-1, Math.min(1, iBytes[i]));
					oData.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7FFF, true);
				}
			}
			return oData;
		};
		function getRawData() { //合并压缩  
			//合并  
			var size = 0;
			for (var i = 0; i < chunks.length; i++) {
				size += chunks[i].length;
			}
			var data = new Float32Array(size);
			var offset = 0;
			for (var i = 0; i < chunks.length; i++) {
				data.set(chunks[i], offset);
				offset += chunks[i].length;
			}
			//压缩
			var getRawDataion = parseInt(audioContext.sampleRate / 16000);
			var length = data.length / getRawDataion;
			var result = new Float32Array(length);
			var index = 0, j = 0;
			while (index < length) {
				result[index] = data[j];
				j += getRawDataion;
				index++;
			}
			return result;
		}
	},
	GetToken: function(unityName){
    var tokenName = "Admin-Token";
	var str = GetCookie(tokenName);//GetCookie写到了index.html里 
	console.log(str);
	//setText(str);//调用index.html里的setText方法
	unityInstance.SendMessage(Pointer_stringify(unityName), "SetTextToken", str);
	function GetCookie(name){
        var strcookie = document.cookie;//获取cookie字符串
		console.log(strcookie);
        var arrcookie = strcookie.split("; ");//分割
        //遍历匹配
        for ( var i = 0; i < arrcookie.length; i++) {

			var arr = arrcookie[i].split("=");
			
			if (arr[0] == name){
				return arr[1];
			}
		}
		return "";
	  }
  },
	ClickSelectFileBtn: function () {
	
		function sendMessageToUnity(s) {
			//发送给unity
			unityInstance.SendMessage("WebSocket", "GetBase64", s);
		}

		//<input type="file" id="files" style="display:none" accept="text/plain" onchange="fileImport()">			

		var doc = document.getElementById('files');
		if (doc == null) {
			doc = document.createElement("input")
			doc.type = "file";
			doc.id = "files";
			doc.style = "display:none";
			doc.accept = "text/plain";
			doc.onchange = fileImport;
			document.body.appendChild(doc);
			console.log("createElement");
		}
		console.log("createElement22");
		doc.click();
	
	function fileImport () {
		console.log("fileImport");
			//获取读取我文件的File对象
			var selectedFile = document.getElementById('files').files[0];
			if (selectedFile != null) {
			console.log(selectedFile);
				var reader = new FileReader();
				reader.readAsDataURL(selectedFile);
				reader.onload = function (e) {
					var base64Str = e.currentTarget.result.substring(e.currentTarget.result.indexOf(',') + 1);
					arr = [];
					step = 3000;
					for (var i = 0, l = base64Str.length; i < l; i += step) {
						arr.push(base64Str.slice(i, i + step))
					}
					sendMessageToUnity("Start");
					for (i = 0; i < arr.length; i++) {
						sendMessageToUnity(arr[i]);
					}
					sendMessageToUnity("End");
				}
			}
		}
},
GetHTTPs :function () 
{
var returnStr = window.location.search;
        var buffer = _malloc(lengthBytesUTF8(returnStr) + 1);
        writeStringToMemory(returnStr, buffer);
        return buffer;
}
};

autoAddDeps(WebSocketLibrary, '$webSocketManager', '$audioInput', '$recorder', '$chunks', '$audioContext');
mergeInto(LibraryManager.library, WebSocketLibrary);
