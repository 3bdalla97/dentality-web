import 'package:firebase_database/firebase_database.dart';
import 'package:http/http.dart' as http;

import '../../../../core/error/exceptions.dart';
import '../../../core/constant/strings.dart';
import '../../models/user/delivery_info_model.dart';

abstract class DeliveryInfoRemoteDataSource {
  Future<List<DeliveryInfoModel>> getDeliveryInfo(String token);
  Future<DeliveryInfoModel> addDeliveryInfo(
    DeliveryInfoModel params,
    String token,
  );
  Future<DeliveryInfoModel> editDeliveryInfo(
    DeliveryInfoModel params,
    String token,
  );
}

class DeliveryInfoRemoteDataSourceImpl implements DeliveryInfoRemoteDataSource {
  final DatabaseReference database;

  DeliveryInfoRemoteDataSourceImpl({required this.database});

  @override
  Future<List<DeliveryInfoModel>> getDeliveryInfo(String userId) async {
    try {
      final snapshot =
          await database.child('users/$userId/delivery-info').get();

      if (snapshot.exists) {
        final data = List<Map<String, dynamic>>.from(
          (snapshot.value as Map)
              .values
              .map((e) => Map<String, dynamic>.from(e)),
        );
        return data.map((json) => DeliveryInfoModel.fromJson(json)).toList();
      } else {
        return []; // No delivery info found, return empty list
      }
    } catch (e) {
      throw ServerException();
    }
  }

  @override
  Future<DeliveryInfoModel> addDeliveryInfo(
      DeliveryInfoModel params, String userId) async {
    try {
      // Generate a unique ID for the new delivery info entry
      final key = database.child('users/$userId/delivery-info').push().key;
      if (key == null) throw ServerException();

      final deliveryInfoMap = params.toJson();
      deliveryInfoMap['_id'] = key; // Add the generated ID to the data

      await database
          .child('users/$userId/delivery-info/$key')
          .set(deliveryInfoMap);

      return DeliveryInfoModel.fromJson(deliveryInfoMap);
    } catch (e) {
      throw ServerException();
    }
  }

  @override
  Future<DeliveryInfoModel> editDeliveryInfo(
      DeliveryInfoModel params, String userId) async {
    try {
      if (params.id == null)
        throw ServerException(); // Ensure ID exists for editing

      await database
          .child('users/$userId/delivery-info/${params.id}')
          .set(params.toJson());

      return params;
    } catch (e) {
      throw ServerException();
    }
  }
}
