import 'package:cached_network_image/cached_network_image.dart';
import 'package:dental/core/extension/string_extension.dart';
import 'package:dental/presentation/blocs/cart/cart_bloc.dart';
import 'package:dental/presentation/blocs/home/navbar_cubit.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_easyloading/flutter_easyloading.dart';

import '../../../core/router/app_router.dart';
import '../../../core/services/services_locator.dart' as di;
import '../../../domain/entities/cart/cart_item.dart';
import '../../../domain/entities/order/order_details.dart';
import '../../../domain/entities/order/order_item.dart';
import '../../blocs/delivery_info/delivery_info_fetch/delivery_info_fetch_cubit.dart';
import '../../blocs/order/order_add/order_add_cubit.dart';
import '../../blocs/user/user_bloc.dart';
import '../../widgets/input_form_button.dart';
import '../../widgets/outline_label_card.dart';

class OrderCheckoutView extends StatefulWidget {
  final List<CartItem> items;
  const OrderCheckoutView({super.key, required this.items});

  @override
  State<OrderCheckoutView> createState() => _OrderCheckoutViewState();
}

class _OrderCheckoutViewState extends State<OrderCheckoutView> {
  int quantity = 0;
  double price = 0.0;
  @override
  void initState() {
    super.initState();
    setQuantityAndPrice();
  }

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<UserBloc, UserState>(
      builder: (context, userState) {
        return BlocProvider(
          create: (context) => di.sl<OrderAddCubit>(),
          child: BlocListener<OrderAddCubit, OrderAddState>(
            listener: (context, state) {
              EasyLoading.dismiss();
              if (state is OrderAddLoading) {
                EasyLoading.show(status: 'Loading...');
              } else if (state is OrderAddSuccess) {
                context.read<NavbarCubit>().update(0);
                context.read<NavbarCubit>().controller.jumpToPage(0);
                context.read<CartBloc>().add(const ClearCart());
                Navigator.of(context).pop();
                EasyLoading.showSuccess("Order Placed Successfully");
              } else if (state is OrderAddFail) {
                EasyLoading.showError("Error");
              }
            },
            child: Scaffold(
              appBar: AppBar(
                backgroundColor: Colors.white,
                title: const Text('Order Checkout'),
              ),
              body: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 20),
                child: ListView(
                  physics: const BouncingScrollPhysics(),
                  children: [
                    const SizedBox(
                      height: 12,
                    ),
                    Stack(
                      children: [
                        Padding(
                          padding: const EdgeInsets.only(top: 8),
                          child: OutlineLabelCard(
                            title: 'Delivery Details',
                            child: BlocBuilder<DeliveryInfoFetchCubit,
                                DeliveryInfoFetchState>(
                              builder: (context, state) {
                                if (state.deliveryInformation.isNotEmpty &&
                                    state.selectedDeliveryInformation != null) {
                                  return Container(
                                    padding: const EdgeInsets.only(
                                        top: 16,
                                        bottom: 12,
                                        left: 4,
                                        right: 10),
                                    child: Column(
                                        mainAxisAlignment:
                                            MainAxisAlignment.spaceEvenly,
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: [
                                          Text(
                                            "${state.selectedDeliveryInformation!.firstName.capitalize()} ${state.selectedDeliveryInformation!.lastName}, ${state.selectedDeliveryInformation!.contactNumber}",
                                            style: const TextStyle(
                                              fontSize: 14,
                                            ),
                                          ),
                                          Text(
                                            "${state.selectedDeliveryInformation!.addressLineOne}, ${state.selectedDeliveryInformation!.addressLineTwo}, ${state.selectedDeliveryInformation!.city}, ${state.selectedDeliveryInformation!.zipCode}",
                                            style: const TextStyle(
                                                fontSize: 14,
                                                fontWeight: FontWeight.w500),
                                          ),
                                        ]),
                                  );
                                } else {
                                  return Container(
                                    height: 50,
                                    padding: const EdgeInsets.only(
                                        top: 20, bottom: 8, left: 4),
                                    child: const Text(
                                      "Please select delivery information",
                                    ),
                                  );
                                }
                              },
                            ),
                          ),
                        ),
                        Positioned(
                          right: -4,
                          top: 0,
                          child: IconButton(
                            onPressed: () {
                              if (userState is UserLogged) {
                                Navigator.of(context)
                                    .pushNamed(AppRouter.deliveryDetails);
                              } else {
                                Navigator.of(context)
                                    .pushNamed(AppRouter.signIn);
                              }
                            },
                            icon: const Icon(
                              Icons.edit,
                              size: 18,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(
                      height: 16,
                    ),
                    OutlineLabelCard(
                      title: 'Selected Products',
                      child: Padding(
                        padding: const EdgeInsets.only(top: 18, bottom: 8),
                        child: Column(
                          children: widget.items
                              .map((product) => Padding(
                                    padding: const EdgeInsets.only(bottom: 8),
                                    child: Row(
                                      children: [
                                        SizedBox(
                                          width: 75,
                                          child: AspectRatio(
                                            aspectRatio: 0.88,
                                            child: ClipRRect(
                                                borderRadius:
                                                    BorderRadius.circular(8.0),
                                                child: Padding(
                                                  padding:
                                                      const EdgeInsets.all(8.0),
                                                  child: CachedNetworkImage(
                                                    imageUrl: product
                                                        .product.images.first,
                                                  ),
                                                )),
                                          ),
                                        ),
                                        const SizedBox(width: 20),
                                        Flexible(
                                          child: Column(
                                            crossAxisAlignment:
                                                CrossAxisAlignment.start,
                                            mainAxisAlignment:
                                                MainAxisAlignment.start,
                                            children: [
                                              Text(
                                                product.product.name,
                                                style: Theme.of(context)
                                                    .textTheme
                                                    .labelLarge,
                                              ),
                                              const SizedBox(
                                                height: 4,
                                              ),
                                              Text(
                                                  'LE${product.priceTag.price.toStringAsFixed(2)}')
                                            ],
                                          ),
                                        ),
                                        Padding(
                                          padding:
                                              const EdgeInsets.only(left: 58.0),
                                          child: Text(
                                              'x ${product.quantity.toString()}'),
                                        )
                                      ],
                                    ),
                                  ))
                              .toList(),
                        ),
                      ),
                    ),
                    const SizedBox(
                      height: 16,
                    ),
                    OutlineLabelCard(
                      title: 'Order Summery',
                      child: Container(
                        height: 120,
                        padding: const EdgeInsets.symmetric(vertical: 8),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                          children: [
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                const Text("Total Number of Items"),
                                Text("x${quantity.toString()}")
                              ],
                            ),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                const Text("Total Price"),
                                Text("LE ${price.toString()}")
                              ],
                            ),
                            // const Row(
                            //   mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            //   children: [
                            //     Text("Delivery Charge"),
                            //     Text("Will Be Determined")
                            //   ],
                            // ),
                            // Row(
                            //   mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            //   children: [
                            //     const Text("Total + Delivery Charge"),
                            //     Text(
                            //         "LE ${(widget.items.fold(0.0, (previousValue, element) => (element.priceTag.price + previousValue)) + 4.99)}")
                            //   ],
                            // )
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(
                      height: 16,
                    ),
                    OutlineLabelCard(
                      title: 'Payment Details',
                      child: Container(
                        padding: const EdgeInsets.symmetric(vertical: 8),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                          children: [
                            // Text(
                            //     "Payment can be made via Cash on Delivery or Vodafone Cash using the number 01055729927, with a payment screenshot sent through WhatsApp."),

                            RichText(
                              text: TextSpan(
                                text:
                                    'Payment can be made via Cash on Delivery or Vodafone Cash using the number ',
                                style: const TextStyle(
                                  color: Colors.black,
                                ),
                                children: <TextSpan>[
                                  TextSpan(
                                    text: '01055729927',
                                    style: const TextStyle(
                                      color: Colors.blue,
                                      fontWeight: FontWeight.bold,
                                      decoration: TextDecoration.underline,
                                      decorationColor: Colors.blue,
                                    ),
                                    recognizer: TapGestureRecognizer()
                                      ..onTap = () async {
                                        Clipboard.setData(const ClipboardData(
                                            text: '01055729927'));
                                        /*ScaffoldMessenger.of(context).showSnackBar(
                                      const SnackBar(
                                        content: Text("01055729927 Copied"),
                                        duration: Duration(
                                            seconds: 2), // Optional: duration
                                      ),
                                    );*/
                                      },
                                  ),
                                  const TextSpan(
                                    text:
                                        ' with a payment screenshot sent through WhatsApp.',
                                    style: TextStyle(
                                      color: Colors.black,
                                    ),
                                  ),
                                ],
                              ),
                            ),

                            const SizedBox(
                              height: 8,
                            ),
                            const Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Padding(
                                  padding: EdgeInsets.only(top: 6.0),
                                  child: Icon(
                                    Icons.circle,
                                    color: Colors.black,
                                    size: 8,
                                  ),
                                ),
                                SizedBox(
                                  width: 4,
                                ),
                                Expanded(
                                  child: Text(
                                      "For other Minya districts, the fee is determined based on the specific district."),
                                )
                              ],
                            ),
                            const SizedBox(
                              height: 4,
                            ),
                            const Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Padding(
                                  padding: EdgeInsets.only(top: 6.0),
                                  child: Icon(
                                    Icons.circle,
                                    color: Colors.black,
                                    size: 8,
                                  ),
                                ),
                                SizedBox(
                                  width: 4,
                                ),
                                Expanded(
                                    child: Text(
                                        "The delivery fee within Minya is 13 EGP."))
                              ],
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              bottomNavigationBar: SafeArea(
                child: Padding(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 20, vertical: 8),
                  child: Builder(builder: (context) {
                    return InputFormButton(
                      color: Colors.black87,
                      onClick: () {
                        if (context
                                .read<DeliveryInfoFetchCubit>()
                                .state
                                .selectedDeliveryInformation ==
                            null) {
                          EasyLoading.showError(
                              "Error \nPlease select delivery add your delivery information");
                        } else {
                          context.read<OrderAddCubit>().addOrder(OrderDetails(
                              id: '',
                              orderItems: widget.items
                                  .map((item) => OrderItem(
                                        id: '',
                                        product: item.product,
                                        priceTag: item.priceTag,
                                        price: item.priceTag.price,
                                        quantity: item.quantity,
                                        date: DateTime.now().toString(),
                                      ))
                                  .toList(),
                              deliveryInfo: context
                                  .read<DeliveryInfoFetchCubit>()
                                  .state
                                  .selectedDeliveryInformation!,
                              discount: 0,
                              date: DateTime.now().toString()));
                        }
                      },
                      titleText: 'Confirm',
                    );
                  }),
                ),
              ),
            ),
          ),
        );
      },
    );
  }

  void setQuantityAndPrice() {
    for (var item in widget.items) {
      quantity += item.quantity;
      price += item.priceTag.price * item.quantity;
    }
  }
}
