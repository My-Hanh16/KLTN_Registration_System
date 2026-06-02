document.querySelectorAll('[data-toggle-reject]').forEach(button => {
            button.addEventListener('click', () => {
                const id = button.getAttribute('data-toggle-reject');
                document.getElementById(`reject-${id}`)?.classList.toggle('open');
            });
        });

