import 'package:flutter/material.dart';

class AboutView extends StatelessWidget {
  const AboutView({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text("About"),
      ),
      body: const Padding(
        padding: EdgeInsets.symmetric(horizontal: 20, vertical: 18),
        child: Text(
          'Specialists in the field of dental materials and their delivery to clinics and doctors within the scope of Minya Governorate and its affiliated villages \n\n' +
              'With the provision of the finest types of materials in the variety required for any clinic or dentist \n\n' +
              'With the presence of the feature of displaying used materials and devices to market them through the app. \n \n',
        ),
      ),
    );
  }
}
