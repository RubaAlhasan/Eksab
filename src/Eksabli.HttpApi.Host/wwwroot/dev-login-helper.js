/* Dev Login Helper */
(() => {
  const defaultAdminUsername = "admin";
  const defaultAdminPassword = "1q2w3E*";

  const run = () => {
    if (!/\/Account\/Login\/?$/i.test(window.location.pathname)) {
      return;
    }

    const userInput = document.querySelector(
      'input[name$="UserNameOrEmailAddress"], input[id$="UserNameOrEmailAddress"], input[name$="UserName"], input[id$="UserName"]'
    );
    const passwordInput = document.querySelector(
      'input[type="password"][name$="Password"], input[type="password"][id$="Password"]'
    );

    const autoFillDefaults = () => {
      if (userInput && !userInput.value) {
        userInput.value = defaultAdminUsername;
      }
      if (passwordInput && !passwordInput.value) {
        passwordInput.value = defaultAdminPassword;
      }
    };

    setTimeout(autoFillDefaults, 150);
    if (userInput) {
      userInput.addEventListener("focus", autoFillDefaults, { once: true });
    }
    if (passwordInput) {
      passwordInput.addEventListener("focus", autoFillDefaults, { once: true });
    }
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", run);
  } else {
    run();
  }
})();