document.addEventListener('DOMContentLoaded', function () {
    const seatMap = document.getElementById('seat-map');
    if (!seatMap) return;

    const selected = new Set();
    seatMap.addEventListener('click', function (e) {
        const btn = e.target.closest('button.seat.available');
        if (!btn) return;
        const id = btn.dataset.seatId;
        if (selected.has(id)) {
            selected.delete(id);
            btn.classList.remove('selected');
        } else {
            selected.add(id);
            btn.classList.add('selected');
        }
        const hidden = document.getElementById('selectedSeatIds');
        if (hidden) hidden.value = Array.from(selected).join(',');
    });

    // If form is submitted, convert selectedSeatIds into multiple inputs for model binding
    const form = document.querySelector('form[asp-action="Create"]') || document.querySelector('form');
    if (form) {
        form.addEventListener('submit', function () {
            const hidden = document.getElementById('selectedSeatIds');
            if (!hidden) return;
            const val = hidden.value;
            // remove previous inputs
            document.querySelectorAll('input[name="selectedSeatIds"]').forEach(i => i.remove());
            if (!val) return;
            val.split(',').forEach(id => {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = 'selectedSeatIds';
                input.value = id;
                form.appendChild(input);
            });
        });
    }
});