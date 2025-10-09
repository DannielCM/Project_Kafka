CREATE DATABASE  IF NOT EXISTS `custom_system` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `custom_system`;
-- MySQL dump 10.13  Distrib 8.0.40, for Win64 (x86_64)
--
-- Host: localhost    Database: custom_system
-- ------------------------------------------------------
-- Server version	8.0.40

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `accounts`
--

DROP TABLE IF EXISTS `accounts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `accounts` (
  `id` int NOT NULL AUTO_INCREMENT,
  `email` varchar(45) NOT NULL,
  `password` varchar(100) NOT NULL,
  `role` varchar(25) NOT NULL,
  `created_at` datetime DEFAULT CURRENT_TIMESTAMP,
  `last_login` datetime DEFAULT NULL,
  `two_factor_secret` varchar(64) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `email_UNIQUE` (`email`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `accounts`
--

LOCK TABLES `accounts` WRITE;
/*!40000 ALTER TABLE `accounts` DISABLE KEYS */;
INSERT INTO `accounts` VALUES (2,'admin@gmail.com','$2a$11$a/0K5pHH68x80/rIi8R5Te3GRpImCSYIoYaR57oqSEr6RKcgfrIaW','admin','2025-09-18 14:23:38','2025-10-08 17:55:52','FZKZAMIWXQGEWGHWU2VRBRUIQ3HC4JPT'),(6,'test@gmail.com','$2a$11$ssxzQ4W4yTBF..Mgbx0UXOVSEwoXeWsefgh2dFEUtq6I5ynDZikUC','basicuser','2025-10-04 06:15:36','2025-10-08 17:49:07',NULL);
/*!40000 ALTER TABLE `accounts` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `students`
--

DROP TABLE IF EXISTS `students`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `students` (
  `id` int NOT NULL AUTO_INCREMENT,
  `student_id` varchar(45) NOT NULL,
  `course` varchar(45) NOT NULL,
  `name` varchar(45) NOT NULL,
  `middle_name` varchar(45) DEFAULT NULL,
  `surname` varchar(45) NOT NULL,
  `date_of_birth` datetime NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `student_id_UNIQUE` (`student_id`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `students`
--

LOCK TABLES `students` WRITE;
/*!40000 ALTER TABLE `students` DISABLE KEYS */;
INSERT INTO `students` VALUES (1,'1001','Computer Science','John','','Doe','2000-05-15 00:00:00'),(2,'1005','Data Science','Tom','','Adams','2002-01-05 00:00:00'),(3,'1007','Animation','Lisa','','Simpson','2003-04-12 00:00:00'),(4,'1009','Photography','Peter','','Parker','2001-01-01 00:00:00');
/*!40000 ALTER TABLE `students` ENABLE KEYS */;
UNLOCK TABLES;

--
-- Table structure for table `tfa_login_request`
--

DROP TABLE IF EXISTS `tfa_login_request`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `tfa_login_request` (
  `id` int NOT NULL AUTO_INCREMENT,
  `token` varchar(100) NOT NULL,
  `account_id` int NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `expires_at` datetime NOT NULL,
  `used` tinyint NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `token_UNIQUE` (`token`),
  KEY `fk_tfa_login_request_account_id_idx` (`account_id`),
  CONSTRAINT `fk_tfa_login_request_account_id` FOREIGN KEY (`account_id`) REFERENCES `accounts` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=31 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Dumping data for table `tfa_login_request`
--

LOCK TABLES `tfa_login_request` WRITE;
/*!40000 ALTER TABLE `tfa_login_request` DISABLE KEYS */;
INSERT INTO `tfa_login_request` VALUES (5,'I6QYUBHcnWaW4HMpn17IvvP7ApMnvZr1Y8h7jqeBb3s=',2,'2025-10-04 22:27:34','2025-10-04 13:32:35',0),(6,'6Nk94HUGxVO0QKo0yj7iWhw6Wm9Np67I4ysQ+2VwDv8=',2,'2025-10-04 22:29:14','2025-10-04 13:34:14',0),(7,'owC/36Tc+StthPN7nejiXqqFf9zc3XDCRrPei0n6XVs=',2,'2025-10-04 22:30:02','2025-10-04 13:35:03',0),(8,'s1g0ZcfNsk+RGAXIRd+X9umcVEXcqBvoE6k/xQykQ6I=',2,'2025-10-04 22:32:03','2025-10-04 13:37:03',0),(9,'4/NBJEmLnQfBsjy/r0lcXNiGKts9fd+cFbZBoyg244M=',2,'2025-10-04 22:32:39','2025-10-04 13:37:40',0),(10,'F0AQox6YvZNFAXR+sOAIo4nTfVgTI6MgQuMmNpRvWUk=',2,'2025-10-04 22:33:01','2025-10-04 13:38:01',0),(11,'xw4i+CtolgVcSYlyECEV+idDhs3rBb44E7e+MpdDZG0=',2,'2025-10-04 22:33:19','2025-10-04 13:38:20',0),(12,'MONY0Tv6qtOlZPx/5BP277FP7rXj6HVcQl4EkVdiLlI=',2,'2025-10-04 22:33:44','2025-10-04 13:38:44',0),(13,'z+ymeWZ0BzGMzsjkgiGOAcrf1cq+YMwkMtiTFso8m90=',2,'2025-10-04 22:34:30','2025-10-04 13:39:31',0),(14,'7d8W2rYX4+s05V8QWqO8iz9MnqKG9cqyQGdbey7ul3g=',2,'2025-10-04 22:35:16','2025-10-04 13:40:17',0),(15,'3bsbUn/C5nlIiq0nl22qj1PkqUDbFzHjfsYPJ/LsIWY=',2,'2025-10-04 22:40:09','2025-10-04 13:45:10',0),(16,'wD7XblFQoGNAqntF06UMf9dOXJHNyGzspsVeJE11q/E=',2,'2025-10-04 22:42:14','2025-10-04 13:47:14',0),(17,'aQFlVz+N1TYvaAAMb1CuQzj+mQ5Dye3C8FuZsnW1VSU=',2,'2025-10-04 23:10:42','2025-10-04 14:15:43',0),(18,'cgwV6Elge6UQ9zidRJRql8P5++e5/lWbaMPVAegdwFE=',2,'2025-10-04 23:15:16','2025-10-04 14:20:16',0),(19,'00EpDm2M81W665/fYsEo/q8zBcrTFELToyFEJ+9ZjW0=',2,'2025-10-04 23:21:34','2025-10-04 14:21:47',1),(20,'MvROWtSqx7ra5tFaXAqfPr35nKGfPIXD/jA24TPGcuM=',2,'2025-10-08 17:40:33','2025-10-08 08:41:19',1),(21,'svKi+zown4qIx13OH2/MCFZbDAMFPVWTXRSmi8u7+CY=',2,'2025-10-08 17:42:14','2025-10-08 08:42:20',1),(22,'uRcRu3x3ALCvo/EVUiP2GxL1j7AxCJem+4cufs4tE90=',2,'2025-10-08 17:44:46','2025-10-08 08:44:51',1),(23,'O+WBuslRpA3dCuRJVXuFtjnS1Qdl9nP+jIlEDtxPkag=',2,'2025-10-08 17:47:38','2025-10-08 08:47:45',1),(24,'gsA2Kct0buaY6N4bwi0mPyWwotRdKjSktfJ2z0W0C2g=',2,'2025-10-08 17:49:28','2025-10-08 08:49:51',1),(25,'FSy4aRc+oL32R4rdtVUcNZ537qvCMTo9YYgMiWChiFU=',2,'2025-10-08 17:52:07','2025-10-08 08:57:08',0),(26,'fko6tJxg3i8ZdrVl7Gm//g0C8NQNdccEqDy6mdK6uyc=',2,'2025-10-08 17:52:12','2025-10-08 08:57:13',0),(27,'uVHfGp+czUEBcHNO5l9SaVwyeF0KfZGw//EMEWRj2nY=',2,'2025-10-08 17:52:12','2025-10-08 08:57:13',0),(28,'d2/JN+FTR6cKY0p7xuTp7sWp6Cx4MQvtLcpLetfoJ1k=',2,'2025-10-08 17:52:14','2025-10-08 08:57:14',0),(29,'jkOk1Nr97rkzg5QsTc6rZ9+YeoIk3ilw2W3+Y6BExGA=',2,'2025-10-08 17:52:34','2025-10-08 08:57:35',0),(30,'S+CCHs7lVq5u5hZzSh8lqHoyk8jW9mpfe1la4rRpQxY=',2,'2025-10-08 17:55:47','2025-10-08 08:55:53',1);
/*!40000 ALTER TABLE `tfa_login_request` ENABLE KEYS */;
UNLOCK TABLES;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2025-10-09 18:33:20
