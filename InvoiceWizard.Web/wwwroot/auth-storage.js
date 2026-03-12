window.invoiceWizardAuth = {
    getSession: function () {
        return window.localStorage.getItem("invoicewizard.auth");
    },
    setSession: function (value) {
        window.localStorage.setItem("invoicewizard.auth", value);
    },
    clearSession: function () {
        window.localStorage.removeItem("invoicewizard.auth");
    }
};
