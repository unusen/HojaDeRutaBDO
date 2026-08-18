(function () {
            const signOutForm = document.getElementById('signOutForm');
            const alertContainer = document.getElementById('globalAlertContainer');

            if (!signOutForm || !alertContainer) {
                return;
            }

            signOutForm.addEventListener('submit', function (event) {
                if (signOutForm.dataset.submitting === 'true') {
                    return;
                }

                event.preventDefault();
                signOutForm.dataset.submitting = 'true';

                alertContainer.innerHTML = `
                    <div class="alert alert-info alert-dismissible fade show" role="alert">
                        Cerrando sesión...
                        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Cerrar"></button>
                    </div>`;

                window.setTimeout(function () {
                    signOutForm.submit();
                }, 250);
            });
        })();
    

