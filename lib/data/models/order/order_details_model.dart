import 'dart:convert';

import '../../../domain/entities/order/order_details.dart';
import '../user/delivery_info_model.dart';
import 'order_item_model.dart';

List<OrderDetailsModel> orderDetailsModelListFromJson(String str) =>
    List<OrderDetailsModel>.from(
        json.decode(str)['data'].map((x) => OrderDetailsModel.fromJson(x)));

List<OrderDetailsModel> orderDetailsModelListFromLocalJson(String str) =>
    List<OrderDetailsModel>.from(
        json.decode(str).map((x) => OrderDetailsModel.fromJson(x)));

OrderDetailsModel orderDetailsModelFromJson(String str) =>
    OrderDetailsModel.fromJson(json.decode(str)['data']);

String orderModelListToJsonBody(List<OrderDetailsModel> data) =>
    json.encode(List<dynamic>.from(data.map((x) => x.toJsonBody())));

String orderModelListToJson(List<OrderDetailsModel> data) =>
    json.encode(List<dynamic>.from(data.map((x) => x.toJson())));

String orderDetailsModelToJson(OrderDetailsModel data) =>
    json.encode(data.toJsonBody());

class OrderDetailsModel extends OrderDetails {
  const OrderDetailsModel({
    required String id,
    required List<OrderItemModel> orderItems,
    required DeliveryInfoModel deliveryInfo,
    required num discount,
    required String date,
  }) : super(
          id: id,
          orderItems: orderItems,
          deliveryInfo: deliveryInfo,
          discount: discount,
          date: date,
        );

  factory OrderDetailsModel.fromJson(Map<String, dynamic> json) =>
      OrderDetailsModel(
        id: json["_id"],
        orderItems: List<OrderItemModel>.from(
            json["orderItems"].map((x) => OrderItemModel.fromJson(x))),
        deliveryInfo: DeliveryInfoModel.fromJson(json["deliveryInfo"]),
        discount: json["discount"],
        date: json["date"],
      );

  Map<String, dynamic> toJson() => {
        "_id": id,
        "orderItems": List<dynamic>.from(
            (orderItems as List<OrderItemModel>).map((x) => x.toJson())),
        "deliveryInfo": (deliveryInfo as DeliveryInfoModel).toJson(),
        "discount": discount,
        "date": date,
      };

  Map<String, dynamic> toJsonBody() => {
        "_id": id,
        "orderItems": List<dynamic>.from(
            (orderItems as List<OrderItemModel>).map((x) => x.toJsonBody())),
        "deliveryInfo": deliveryInfo.id,
        "discount": discount,
        "date": date,
      };

  factory OrderDetailsModel.fromEntity(OrderDetails entity) =>
      OrderDetailsModel(
          id: entity.id,
          orderItems: entity.orderItems
              .map((orderItem) => OrderItemModel.fromEntity(orderItem))
              .toList(),
          deliveryInfo: DeliveryInfoModel.fromEntity(entity.deliveryInfo),
          discount: entity.discount,
          date: entity.date);

  OrderDetailsModel copyWith({
    String? id,
    List<OrderItemModel>? orderItems,
    DeliveryInfoModel? deliveryInfo,
    num? discount,
    String? date,
  }) {
    return OrderDetailsModel(
      id: id ?? this.id,
      orderItems: orderItems ?? this.orderItems as List<OrderItemModel>,
      deliveryInfo: deliveryInfo ?? this.deliveryInfo as DeliveryInfoModel,
      discount: discount ?? this.discount,
      date: date ?? this.date,
    );
  }
}
