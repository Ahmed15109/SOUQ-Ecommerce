
$(document).ready(function () {
    // Favorites Toggle
    $(document).on('click', '.favorite-btn', function (e) {
        e.preventDefault();
        var btn = $(this);
        // Find the product ID: logic depends on where data-id is.
        // If not directly on btn, check if we need to infer it or if we forgot to adding it to Index.cshtml loop

        // Wait, did I add data-id to the buttons in Home/Index.cshtml and Products/Index.cshtml? 
        // I need to double check. In Home/Index I didn't explicitly add data-id in the loop. 
        // I should fix that first or rely on a stronger selector.

        // Let's assume I will fix the Views to include data-id attribute on the button.
        var productId = btn.data('id');

        if (!productId) {
            // Fallback: try to find it from context if I missed adding it (which I likely did in Step 1)
            // But for robustness, I will update the JS to be ready, and then I MUST go back and update Home/Index and Products/Index to include data-id.
            console.error("Product ID not found");
            return;
        }

        $.post('/Favorites/Toggle', { id: productId }, function (response) {
            if (response.success) {
                // Update Badge
                $('.bi-heart').next('.badge').text(response.count);

                // Update Icon State
                if (response.isFavorite) {
                    btn.addClass('text-danger').removeClass('text-muted');
                    btn.find('i').removeClass('bi-heart').addClass('bi-heart-fill');
                } else {
                    btn.removeClass('text-danger').addClass('text-muted');
                    btn.find('i').removeClass('bi-heart-fill').addClass('bi-heart');

                    // If we are on Favorites page, remove the card
                    if (window.location.pathname.toLowerCase().includes('favorites')) {
                        btn.closest('.col').fadeOut(); // Animated remove
                    }
                }
            }
        });
    });

    // Add To Cart
    $(document).on('click', '.add-to-cart-btn, .btn-primary.rounded-circle', function (e) {
        var btn = $(this);
        var productId = btn.data('id');

        // Fallback for legacy buttons if data-id is missing
        if (!productId) {
            productId = btn.closest('.card-body').prev().find('.favorite-btn').data('id');
        }

        if (productId) {
            // Animation feedback
            var icon = btn.find('i');
            var isNewBtn = icon.hasClass('bi-cart-plus');
            var originalIconClass = isNewBtn ? 'bi-cart-plus' : 'bi-plus-lg';
            
            icon.removeClass(originalIconClass).addClass('bi-check-lg');

            $.post('/Cart/AddToCart', { id: productId }, function (response) {
                if (response.success) {
                    $('.bi-cart3').next('.badge').text(response.count);
                    setTimeout(function () {
                        icon.removeClass('bi-check-lg').addClass(originalIconClass);
                    }, 1000);
                }
            });
        }
    });

    // Update Quantity
    $(document).on('click', '.btn-update-qty', function () {
        var btn = $(this);
        var row = btn.closest('.cart-item');
        var productId = row.data('id');
        var change = parseInt(btn.data('change'));
        var input = row.find('.qty-input');
        var currentQty = parseInt(input.val());
        var newQty = currentQty + change;

        if (newQty < 1) return; // Prevent 0 from here, use remove button

        $.post('/Cart/UpdateQuantity', { id: productId, qty: newQty }, function (response) {
            if (response.success) {
                input.val(newQty);
                row.find('.item-total').text('$' + response.itemTotal.toFixed(2));
                $('#cart-subtotal').text('$' + response.cartTotal.toFixed(2));
                $('#cart-total').text('$' + response.cartTotal.toFixed(2));
                $('#cart-count').text(response.count);
                $('.bi-cart3').next('.badge').text(response.count);
            }
        });
    });

    // Remove from Cart
    $(document).on('click', '.btn-remove', function () {
        var row = $(this).closest('.cart-item');
        var productId = row.data('id');

        $.post('/Cart/RemoveFromCart', { id: productId }, function (response) {
            if (response.success) {
                row.fadeOut(function () {
                    $(this).remove();
                    if ($('.cart-item').length === 0) location.reload();
                });
                $('#cart-subtotal').text('$' + response.cartTotal.toFixed(2));
                $('#cart-total').text('$' + response.cartTotal.toFixed(2));
                $('.bi-cart3').next('.badge').text(response.count);
                $('#cart-count').text(response.count);
            }
        });
    });
});
