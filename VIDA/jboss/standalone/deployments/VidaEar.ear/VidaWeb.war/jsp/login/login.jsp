<%@ page language="java" contentType="text/html; charset=utf-8"%>
<%@ page errorPage="/jsp/error.jsp" %>
<%@ taglib uri="/struts-html" prefix="html" %>
<%@ taglib uri="/struts-logic" prefix="logic" %>
<%@ taglib uri="/struts-bean" prefix="bean" %>
<%@ taglib uri="/vida-access" prefix="access" %>
<%@ include file="/jsp/nocache.jsp" %>

<link rel=stylesheet href="${pageContext.request.contextPath}/CSS/parts.css" type="text/css" />
<html>
<HEAD>
<meta http-equiv="X-UA-Compatible" content="IE=5">
<!-- login.jsp -->
<script language="JavaScript" src="${pageContext.request.contextPath}/JavaScripts/pagetoolbar.js"></script>

<TITLE>VIDA</TITLE>
<logic:equal name="loginForm" property="cas" value="false">
<object id="DotNetPreLoader" classid="<bean:write name="loginForm" property="rootUrl" />/Diagnostics/UI.DLL#Vcc.Vida.DiagSwdl.UI.DotNetPreLoader" width="0" height="0" codetype="application/x-msdownload">
	<param name="RootUrl" value="<bean:write name="loginForm" property="rootUrl" />/Diagnostics/"/>
	<param name="LogPath" value="<bean:write name="loginForm" property="logPath" />">
	<param name="CultureName" value="en-GB">
	<param name="TitleName" value="<bean:write name="loginForm" property="publicationVersion" />">
	
</object>
</logic:equal>
<access:script/>

<script language="javascript">
	function checkUserName() {
		var theForm = document.forms[0];
		var usernameInput = document.getElementById("usernameId") || theForm.username;
		var username = usernameInput ? usernameInput.value.replace(/^\s+|\s+$/g, '') : "";
		
		theForm.actionEvent.value = "login";
		if (theForm.hostName && (!theForm.hostName.value || theForm.hostName.value.length == 0)) {
			theForm.hostName.value = document.location.hostname || "localhost";
		}
		if(username.length > 0) {
			theForm.username.value = username;
			theForm.username.disabled = false;
			openProgressFrameLogin();
			<logic:equal name="loginForm" property="cas" value="false">
			try{
				DotNetPreLoader.Start();
			} catch(e){
				//ignore error
			}
			</logic:equal>
			theForm.submit();
		} else {
			alert("Please enter a username (e.g. 1 or admin).");
		}
	}

	function launchRegistrationWizard() {
		var button = document.getElementById("computerRegistrationWizard");
		if(button.disabled == true) {
			return;
		}
		var theForm = document.forms[0];
		theForm.action = '/Vida/RegistrationWizard.do';
		theForm.username.value = ".";
		theForm.submit();
	}

	function loginOnLoad() {
		//Set host name in loginForm
		document.forms[0].hostName.value = document.location.hostname;		
	
		if(window.opener == null) {
			var userName = document.getElementById("usernameId");
			<logic:equal name="loginForm" property="userNameLocked" value="true">
				userName.disabled = true;
			</logic:equal>
			setFocus();
			document.onkeypress = checkUserNameLength;
			var loginButton = document.getElementById("loginId");
			if (userName.value.length == 0 && loginButton != null) {
				loginButton.disabled = true;
			}
		} else {
			window.opener.location.href = "LogOut.do";
			window.close();
		}
	}

	function checkScreenResolution() {
		var width = screen.availWidth;
		var height = screen.availHeight;
		var imgText;
		if(width>1152) {
			imgText = "<img src=\"Images/login_topimage_im_large.jpg\" style=\"background-color:white;width:100%\"/>";
		} else if(width>928) {
			imgText = "<img src=\"Images/login_topimage_im.jpg\" style=\"background-color:white;width:100%\"/>";
		} else {
			imgText = "<img src=\"Images/login_topimage_im_small.jpg\" style=\"background-color:white;width:100%\"/>";
		}
		document.write(imgText);
	}

	function setFocus() {
		try {
			document.forms[0].username.focus();
		}catch (Exception)
		{}
	}

	function check_loginbutton() {
		var loginButton = document.getElementById("loginId");
		var userName = document.getElementById("usernameId");
		if (loginButton.disabled == true) {
			if (userName.value.length> 0) {
				loginButton.disabled = false;
			}
		} else {
			if (userName.value.length == 0) {
				loginButton.disabled = true;
			}
		}
	}

	function checkUserNameLength(e) {
		var myKeyCode = document.all? window.event.keyCode:e.which;
		var userName = document.getElementById("usernameId");

		if (userName.value.length==0) {
			return (myKeyCode != 13);
		} else {
			if (myKeyCode == 13) {
				checkUserName();
				return false;
			} else {
				return true;
			}
		}
	}
</script>


</HEAD>
<BODY onload="loginOnLoad()" style="margin:0px;padding:0px;border-width:1;border-style:solid; background-color:#F5F5F5;">
<div style="background-color: white;vertical-align:middle;width:100%;">
	<script language="javascript">
		checkScreenResolution();
	</script>
	<img src="Images/login_vida.gif" align="left" style="vertical-align:middle;padding: 35px;"/>
</div>
<html:form action="Login" method="POST" onsubmit="checkUserName(); return false;">
<html:hidden property="hostName"/>
<div style="padding-right: 265px;">
	
	<div align="right" STYLE="padding-left:10px;padding-top:10px;">
		<access:error/>
	</div>

	<table align="right" CELLPADDING="5" CELLSPACING="0" BORDER="0">
		<tr>
			<td nowrap>
				<table cellpadding="2" cellspacing="4">
					<tr>
						<td></td>
						<td class="text">
							<IFRAME style="background-color:#F5F5F5" height="49px" width="120px" marginwidth="0px" ID="progressFrame" FRAMEBORDER=0 SCROLLING=NO SRC="jsp/empty.html"></IFRAME>
						</td>
					</tr>
					<tr>
						<td class="text" align="right">
							<bean:message key="lblUserName"/>
						</td>
						<td>
							<html:text onkeyup="check_loginbutton();" onkeydown="if(event.keyCode==13){checkUserName();return false;}" styleId="usernameId" styleClass="text" name="loginForm" property="username" size="16" maxlength="16"/>
							<logic:equal name="loginForm" property="computerName" value="">
								<script>								
									document.getElementById("usernameId").readOnly = true;
									document.getElementById("usernameId").className = "disabled";
								</script>
							</logic:equal>
						</td>
					</tr>

					<tr>
						<td colspan="2" height="60" align="right">
							<html:hidden property="actionEvent"/>
							<access:actionButton id="loginId" javaScriptName="checkUserName"><bean:message key="cmdLogin" /></access:actionButton> <input type="submit" value="Log in" style="margin-left:10px; padding:4px 16px; font-weight:bold; background-color:#003057; color:white; border:1px solid #002040; border-radius:3px; cursor:pointer;" onclick="checkUserName(); return false;" />
						</td>
					</tr>

					<bean:define id="computerName" name="loginForm" property="computerName" type="String" />
					<bean:define id="publicationVersion" name="loginForm" property="publicationVersion" type="String" />

					<tr>
						<td class="text">
							<logic:equal name="loginForm" property="aioc" value="true">
								<access:actionButton id="computerRegistrationWizard" javaScriptName="launchRegistrationWizard" javaScriptArg=""><bean:message key="uc.regwizard.lblTitle"/></access:actionButton><br/>
							</logic:equal>
								&nbsp;<br/>
							<logic:notEqual name="loginForm" property="computerName" value="">
								Problems with log in?<br/>
								Please contact your local helpdesk<br/>
								&nbsp<br/>
								<bean:message key="lblComputerName" arg0="<%=computerName%>"/>
							</logic:notEqual>
							&nbsp<br/>
							<bean:message key="lblCurrentBild"/> <bean:write name="loginForm" property="publicationVersion" /> <br/>
						</td>
					</tr>
				</table>
		</table>
</div>
</html:form>
</BODY>
</html>
