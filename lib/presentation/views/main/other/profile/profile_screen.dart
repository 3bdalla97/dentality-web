import 'package:dental/core/constant/images.dart';
import 'package:dental/presentation/blocs/cart/cart_bloc.dart';
import 'package:dental/presentation/blocs/delivery_info/delivery_info_fetch/delivery_info_fetch_cubit.dart';
import 'package:dental/presentation/blocs/order/order_fetch/order_fetch_cubit.dart';
import 'package:dental/presentation/blocs/user/user_bloc.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../../domain/entities/user/user.dart';
import '../../../../widgets/input_form_button.dart';
import '../../../../widgets/input_text_form_field.dart';

class UserProfileScreen extends StatefulWidget {
  final User user;
  const UserProfileScreen({super.key, required this.user});

  @override
  State<UserProfileScreen> createState() => _UserProfileScreenState();
}

class _UserProfileScreenState extends State<UserProfileScreen> {
  final TextEditingController firstNameController = TextEditingController();
  final TextEditingController lastNameController = TextEditingController();
  final TextEditingController email = TextEditingController();

  @override
  void initState() {
    super.initState();
    firstNameController.text = widget.user.firstName;
    lastNameController.text = widget.user.lastName;
    email.text = widget.user.email;
  }

  void _confirmDeleteAccount(BuildContext context) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text("Delete Account"),
        content: const Text("Are you sure you want to delete your account?"),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text("Cancel"),
          ),
          TextButton(
            onPressed: () {
              Navigator.of(ctx).pop(); // Close the dialog

              Future.microtask(() {
                context.read<UserBloc>().add(SignOutUser());
                context.read<CartBloc>().add(const ClearCart());
                context.read<DeliveryInfoFetchCubit>().clearLocalDeliveryInfo();
                context.read<OrderFetchCubit>().clearLocalOrders();

                // Navigate to login or initial route
                Navigator.of(context).pushNamedAndRemoveUntil('/login', (route) => false);
              });
            },
            child: const Text("Delete", style: TextStyle(color: Colors.red)),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: Theme.of(context).scaffoldBackgroundColor,
        title: const Text('Profile'),
      ),
      body: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 25),
        child: ListView(
          children: [
            Hero(
              tag: "C001",
              child: CircleAvatar(
                radius: 75.0,
                backgroundColor: Colors.grey.shade200,
                child: Image.asset(kUserAvatar),
              ),
            ),
            const SizedBox(height: 50),
            InputTextFormField(
              controller: firstNameController,
              hint: 'First Name',
            ),
            const SizedBox(height: 12),
            InputTextFormField(
              controller: lastNameController,
              hint: 'Last Name',
            ),
            const SizedBox(height: 12),
            InputTextFormField(
              controller: email,
              enable: false,
              hint: 'Email Address',
            ),
            const SizedBox(height: 52),
            Center(
              child: MaterialButton(
                color: Colors.red,
                minWidth: double.infinity,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(8)
                ),
                onPressed: () => _confirmDeleteAccount(context),
                child: const Text(
                  'Delete Account',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 16,
                    fontWeight: FontWeight.w500,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
      // bottomNavigationBar: SafeArea(
      //   child: Padding(
      //     padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
      //     child: InputFormButton(
      //       onClick: () {
      //         // Add update profile logic here
      //       },
      //       titleText: "Update",
      //       color: Colors.black87,
      //     ),
      //   ),
      // ),
    );
  }
}
