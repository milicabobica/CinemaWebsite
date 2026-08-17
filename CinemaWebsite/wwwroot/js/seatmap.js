document.addEventListener('DOMContentLoaded', function () {
    const seatMap = document.getElementById('seat-map');
    if (!seatMap) return;

    const pricePerSeat = parseFloat(seatMap.dataset.price || '0');
    const selected = new Set();

    function updateSummary() {
        const selectedCountEl = document.getElementById('selectedCount');
        const totalPriceEl = document.getElementById('totalPrice');
        const seatPriceEl = document.getElementById('seatPrice');
        const ticketInput = document.querySelector('input[name="NumberOfTickets"]');

        const count = selected.size;
        if (selectedCountEl) selectedCountEl.textContent = String(count);
        if (seatPriceEl) seatPriceEl.textContent = pricePerSeat.toFixed(2);
        if (totalPriceEl) totalPriceEl.textContent = (pricePerSeat * count).toFixed(2);
        if (ticketInput) ticketInput.value = String(count === 0 ? ticketInput.value : count);

        const hidden = document.getElementById('selectedSeatIds');
        if (hidden) hidden.value = Array.from(selected).join(',');
    }

    seatMap.addEventListener('click', function (e) {
        const btn = e.target.closest('button.seat.available');
        if (!btn) return;
        const id = btn.dataset.seatId;

        if (!selected.has(id)) {
            selected.add(id);
            btn.classList.add('selected');
        } else {
            selected.delete(id);
            btn.classList.remove('selected');
        }
        updateSummary();
    });

    // when number of tickets input exists, keep it in sync: selections update it; manual edits won't be enforced here
    const ticketInputEl = document.querySelector('input[name="NumberOfTickets"]');

    // if form is submitted, convert selectedSeatIds into multiple inputs for model binding
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

    // initialize summary in case seats are pre-selected
    updateSummary();
});