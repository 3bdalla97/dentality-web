import 'dart:convert';

import 'package:dental/core/error/failures.dart';
import 'package:dental/data/models/user/user_model.dart';
import 'package:firebase_database/firebase_database.dart';
import 'package:http/http.dart' as http;

import '../../../../core/error/exceptions.dart';
import '../../../core/constant/strings.dart';
import '../../../domain/usecases/user/sign_in_usecase.dart';
import '../../../domain/usecases/user/sign_up_usecase.dart';
import '../../models/user/authentication_response_model.dart';

abstract class UserRemoteDataSource {
  Future<AuthenticationResponseModel> signIn(SignInParams params);
  Future<AuthenticationResponseModel> signUp(SignUpParams params);
}

// Replace with your actual exception
// Replace with your actual exceptions

// Replace with your actual exceptions
// Replace with your actual exceptions

class UserRemoteDataSourceImpl implements UserRemoteDataSource {
  final DatabaseReference database;

  UserRemoteDataSourceImpl({required this.database});

  // Generate a simple token (not secure, consider using JWT for production)
  String _generateToken(String userId) {
    final timestamp = DateTime.now().millisecondsSinceEpoch.toString();
    return base64Url.encode(utf8.encode('$userId:$timestamp'));
  }

  @override
  Future<AuthenticationResponseModel> signIn(SignInParams params) async {
    try {
      // Fetch all users from Firebase
      final snapshot = await database.child('users').get();

      if (snapshot.exists) {
        // Iterate through all children to find a match
        for (final child in snapshot.children) {
          final userData = Map<String, dynamic>.from(child.value as Map);

          if (userData['email'] == params.username &&
              userData['password'] == params.password) {
            // Found the user, construct the UserModel
            final user = UserModel.fromJson(userData);
            final token = user.id;

            return AuthenticationResponseModel(
              token: token,
              user: user,
            );
          }
        }
        // If no match is found
        throw CredentialFailure();
      } else {
        throw CredentialFailure();
      }
    } catch (e) {
      throw ServerException();
    }
  }

  @override
  Future<AuthenticationResponseModel> signUp(SignUpParams params) async {
    try {
      // Generate a unique ID for the new user
      final uid = database.child('users').push().key;
      if (uid == null) throw ServerException();

      // Create user data
      final userMap = {
        "_id": uid,
        "firstName": params.firstName,
        "lastName": params.lastName,
        "email": params.email,
        "password": params.password, // Note: Hash passwords for security
      };

      // Save user data in the Firebase Realtime Database
      await database.child('users/$uid').set(userMap);

      // Convert to UserModel
      final user = UserModel.fromJson(userMap);
      final token = user.id;

      return AuthenticationResponseModel(
        token: token,
        user: user,
      );
    } catch (e) {
      throw ServerException();
    }
  }
}
