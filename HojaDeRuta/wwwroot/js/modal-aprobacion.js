document.addEventListener("DOMContentLoaded", function () {
    var modalElement = document.getElementById("modalMensaje");
    if (modalElement) {
        var modal = new bootstrap.Modal(modalElement);
        modal.show();
    }
});

